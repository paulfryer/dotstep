using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Amazon.CDK;
using Amazon.CDK.AWS.StepFunctions;
using DotStep.Core.States;
using Newtonsoft.Json;
using IStateMachine = DotStep.Core.StateMachines.IStateMachine;
using State = Amazon.CDK.AWS.StepFunctions.State;

namespace DotStep.Core
{
    public class StepFunctionGenerator<TStateMachine> where TStateMachine : IStateMachine
    {
        private readonly App app;
        private readonly StateMachineProps SMP = new StateMachineProps();
        private readonly Stack stack;
        private readonly IState StartState;
        private StateMachine sfStateMachine;

        public StepFunctionGenerator()
        {
            var stateMachine = Activator.CreateInstance<TStateMachine>();
            StartState = stateMachine.GetStartState();

            app = new App();
            stack = new Stack(app);
        }

        public void GenerateStateMachine()
        {
            AddState(StartState);

            sfStateMachine = new StateMachine(stack, typeof(TStateMachine).Name, SMP);

            var cloudAssembly = app.Synth();

            var directory = new DirectoryInfo(cloudAssembly.Directory);

            var templateFile = directory.GetFiles("*.template.json").Single();

            var templateJson = File.ReadAllText(templateFile.FullName);
            var template = JsonConvert.DeserializeObject<dynamic>(templateJson);


            var description = (string)(template.Resources as IEnumerable<dynamic>)
                .Single(r => r.Value.Type == "AWS::StepFunctions::StateMachine")
                .Value.Properties.DefinitionString;

            var deserializedVersion = JsonConvert.DeserializeObject<dynamic>(description);
            var serializedVersion = JsonConvert.SerializeObject(deserializedVersion, Formatting.Indented);

            Console.Write(serializedVersion);
        }

        private State AddState(IState state, State parentState = null)
        {
            State newState;

            switch (state)
            {
                case ParallelState parallelState:
                    newState = new Parallel(stack, state.Name, new ParallelProps());
                    var branches = new List<IChainable>();
                    foreach (var childState in parallelState.States) branches.Add(AddState(childState));
                    (newState as Parallel).Branch(branches.ToArray());
                    break;
                case IMapState mapState:

                    var firstStateOfMap = AddState(mapState.StartState);
                    newState = new Map(stack, state.Name, new MapProps()).Iterator(firstStateOfMap);


                    break;
                case IAmazonStateTask amazonState:
                    var parameters = amazonState.Parameters;

                    newState = new CustomState(stack, state.Name, new CustomStateProps
                    {
                        StateJson = new Dictionary<string, object>
                        {
                            { "Type", "Task" },
                            { "Resource", amazonState.Arn },
                            { "Parameters", parameters }
                        }
                    });

                    if (amazonState.ErrorHandlers.Any())
                    {
                        //(newState as TaskStateBase).Add()
                    }

                    break;
                case SuccessState successState:
                    newState = new Succeed(stack, state.Name, new SucceedProps
                    {
                        Comment = successState.Comment
                    });
                    break;
                default: throw new NotSupportedException(state.GetType().Name);
            }

            if (parentState is null)
                SMP.Definition = newState;
            else
                (parentState as INextable).Next(newState);


            if (state is ITransitionalState transitionalState && transitionalState.NextState != null)
                AddState(transitionalState.NextState, newState);

            return newState;
        }
    }
}