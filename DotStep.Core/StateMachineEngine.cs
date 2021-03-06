﻿using System;
using System.Threading.Tasks;
using System.Reflection;
using System.Collections.Generic;

namespace DotStep.Core
{
    public class StateMachineEngine<TStateMachine, TContext>
        where TStateMachine : IStateMachine
        where TContext : IContext
    {
        TStateMachine stateMachine;
        TContext context;

        public StateMachineEngine(TContext context)
        {
            stateMachine = (TStateMachine)Activator.CreateInstance(typeof(TStateMachine));
            this.context = context;
        }

        public async Task Start()
        { 
            await ChangeState(stateMachine.StartAt);
        }

        private async Task ChangeState(Type type)
        {
            try {

                Console.WriteLine("Changing state to: " + type.Name);

                var state = Activator.CreateInstance(type) as IState;

                if (state.End)                
                    return;                

                if (state is ITaskState<TContext>)                
                {
                    var taskState = state as ITaskState<TContext>;
                    context = await taskState.Execute(context);
                    if (!taskState.End)
                        await ChangeState(taskState.Next);
                }
                else if (state is IChoiceState)
                {
                    var choiceState = state as IChoiceState;
                    var useDefault = true;
                    foreach (var choice in choiceState.Choices)
                    {
                        var useNext = false;
                        var compairValue = typeof(TContext).GetProperty(choice.Variable).GetValue(context);
                        var operatorStart = choice.Operator.Substring(0, 2).ToUpper();                        
                        switch (operatorStart)
                        {
                            case "BO":
                                if (Convert.ToBoolean(compairValue) == Convert.ToBoolean(choice.Value))
                                    useNext = true;                                  
                                break;
                            case "NU":
                                var numericCompairValue = Convert.ToDecimal(compairValue);
                                var numericValue = Convert.ToDecimal(choice.Value);
                                switch (choice.Operator)
                                {
                                    case Operator.NumericEquals:
                                        if (numericCompairValue == numericValue)
                                            useNext = true;                                           
                                        break;
                                    case Operator.NumericGreaterThan:
                                        if (numericCompairValue > numericValue)
                                            useNext = true;
                                        break;
                                    case Operator.NumericGreaterThanEquals:
                                        if (numericCompairValue >= numericValue)
                                            useNext = true;
                                        break;
                                    case Operator.NumericLessThan:
                                        if (numericCompairValue < numericValue)
                                            useNext = true;
                                        break;
                                    case Operator.NumericLessThanEquals:
                                        if (numericCompairValue <= numericValue)
                                            useNext = true;
                                        break;
                                    default: throw new NotImplementedException("Not implemented: " + choice.Operator);
                                }
                                break;
                            case "ST":
                                var stringComapirValue = Convert.ToString(compairValue);
                                var stringValue = Convert.ToString(choice.Value);
                                switch (choice.Operator)
                                {
                                    case Operator.StringEquals:
                                        if (stringComapirValue == stringValue)
                                            useNext = true;
                                        break;
                                    default: throw new NotImplementedException("Not implemented: " + choice.Operator);
                                }

                                break;
                            default: throw new NotImplementedException("Operator not supported: " + choice.Operator);
                        }
                        if (useNext)
                        {
                            useDefault = false;
                            await ChangeState(choice.Next);
                            break;
                        }
                        
                    }
                    if (useDefault)
                        await ChangeState(choiceState.Default);
                }
                else if (state is IWaitState)
                {
                    var waitState = state as IWaitState;
                    await Task.Delay(waitState.Seconds * 1000);
                    await ChangeState(waitState.Next);
                }
                else if (state is IPassState)
                {
                    // nothing to do here..
                    var passSate = state as IPassState;
                    
                }
                else if (state is IParallelState<TContext>) {
                    var parallelState = state as IParallelState<TContext>;
                    var tasks = new List<Task>();
                    foreach (var stateType in parallelState.ParallelStateTypes) 
                        tasks.Add(ChangeState(stateType));                    
                    await Task.WhenAll(tasks);
                    await ChangeState(parallelState.Next);
                }
                else throw new NotImplementedException("State type not implemented: " + type.Name);

                

            } catch (Exception exception) {
                Console.WriteLine(exception.Message);
                throw;
            }
           
        }
    }
}
