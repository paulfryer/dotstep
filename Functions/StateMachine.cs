using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace DotStep.Core
{
    public abstract class StateMachine<TStartsAt> : IStateMachine 
        where TStartsAt : IState
    {
        public Type StartAt {
            get { 
                return this.GetType().GetTypeInfo().BaseType.GetGenericArguments()[0]; 
            }
        }

        private IEnumerable<IState> States
        {
            get
            {
                return Assembly.GetEntryAssembly().GetTypes()
                                     .Where(t => typeof(IState).IsAssignableFrom(t) &&
                                            t.GetTypeInfo().IsClass &&
                                            t.GetTypeInfo().IsSealed &&
                                            t.Namespace == StartAt.Namespace)
                                         .Select(t => (IState)Activator.CreateInstance(t));
            }
        }

        public string Describe(string region, string accountId)
        {
            

            var sb = new StringBuilder();

            sb.AppendLine("{");
            sb.AppendLine("\"StartAt\": \"" + StartAt.Name + "\",");
            sb.AppendLine("\"States\": {");

            
            var appendComma = false;
            foreach (var state in States){
                if (appendComma) sb.Append(",");
                DescribeState(sb, state, region, accountId);
                appendComma = true;
            }
   
            sb.Append("}");
            sb.AppendLine("}");

            return sb.ToString();

        }

        public string Publish(string region, string accountId)
        {

            foreach (var state in States) {

            }

            var buildProcess = Process.Start(new ProcessStartInfo("dotnet", "publish") {
                
            });

            while (!buildProcess.HasExited) {
                Console.WriteLine("Waiting for dotnet publish to complete. " + DateTime.UtcNow.ToString());
                Thread.Sleep(250);
            }


            throw new NotImplementedException();
        }

        void DescribeState(StringBuilder sb, IState state, string region, string accountId)
        {
            sb.AppendLine("\"" + state.GetType().Name + "\" : { ");

            if (state is ITaskState)
            {
                var taskState = state as ITaskState;
                sb.AppendLine("\"Type\":\"Task\",");
                sb.AppendLine($"\"Resource\":\"arn:aws:lambda:{region}:{accountId}:function:{GetType().Name}-{state.Name}\",");
                sb.AppendLine($"\"Next\":\"{taskState.Next.Name}\"");
            }
            if (state is IChoiceState){
                var choiceState = state as IChoiceState;
                sb.AppendLine("\"Type\":\"Choice\",");
                sb.AppendLine("\"Choices\": [");
                var appendComma = false;
                foreach(var choice in choiceState.Choices){
                    if (appendComma) sb.Append(",");
                    sb.AppendLine("{");
                    sb.AppendLine("\"Variable\":\"$." + choice.Variable + "\",");
                    var stringValue = Convert.ToString(choice.Value);
                    if (choice.Operator.ToUpper().StartsWith("ST"))
                        stringValue = "\"" + stringValue + "\"";
                    if (choice.Operator.ToUpper().StartsWith("BO"))
                        stringValue = stringValue.ToLower();
                    sb.AppendLine($"\"{choice.Operator}\": {stringValue},");
                    sb.AppendLine($"\"Next\":\"{choice.Next.Name}\"");
                    sb.AppendLine("}");
                    appendComma = true;
                }
                sb.AppendLine("]");
            }
            if (state is IPassState){
                sb.AppendLine("\"Type\":\"Pass\",");
            }
            if (state is IWaitState){
                var waitState = state as IWaitState;
                sb.AppendLine("\"Type\":\"Wait\",");
                sb.AppendLine("\"Seconds\": " + waitState.Seconds + ",");
                sb.AppendLine($"\"Next\":\"{waitState.Next.Name}\"");

            }

            if (state.End)
                sb.AppendLine("\"End\": true");

            sb.AppendLine("}");
        }
    }


}
