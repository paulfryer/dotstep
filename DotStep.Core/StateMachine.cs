using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DotStep.Core
{
    public abstract class StateMachine<TStartsAt> : IStateMachine
        where TStartsAt : IState
    {
        public Type StartAt
        {
            get
            {       
                return this.GetType().GetTypeInfo().BaseType.GetGenericArguments()[0];
            }
        }


        public IEnumerable<Type> StateTypes
        {
            get
            {
                var nestedTypes = GetType().GetTypeInfo().GetMembers()
                    .Where(member => member.MemberType == MemberTypes.NestedType);

                foreach (dynamic nestedType in nestedTypes)
                {
                    if ((nestedType.ImplementedInterfaces as Type[]).Any(t => typeof(IState).IsAssignableFrom(t)))
                        yield return nestedType;
                }


                /*
             &&
                typeof(IState).IsAssignableFrom(member.DeclaringType))
                .Select(t => (IState)Activator.CreateInstance(t.DeclaringType));
                */

                /*
                return GetType().GetTypeInfo().Assembly.GetTypes()
                                     .Where(t => typeof(IState).IsAssignableFrom(t) &&
                                            t.GetTypeInfo().IsClass &&
                                            t.GetTypeInfo().IsSealed &&
                                            t.Namespace == StartAt.Namespace)
                                         .Select(t => (IState)Activator.CreateInstance(t));*/
            }
        }

        public IEnumerable<IState> States
        {
            get
            {
                var nestedTypes = GetType().GetTypeInfo().GetMembers()
                    .Where(member => member.MemberType == MemberTypes.NestedType);
                
                foreach (dynamic nestedType in nestedTypes) {
                    if ((nestedType.ImplementedInterfaces as Type[]).Any(t => typeof(IState).IsAssignableFrom(t)))
                        yield return (IState)Activator.CreateInstance(nestedType);
                }
                                 
                    
                    /*
                 &&
                    typeof(IState).IsAssignableFrom(member.DeclaringType))
                    .Select(t => (IState)Activator.CreateInstance(t.DeclaringType));
                    */

                /*
                return GetType().GetTypeInfo().Assembly.GetTypes()
                                     .Where(t => typeof(IState).IsAssignableFrom(t) &&
                                            t.GetTypeInfo().IsClass &&
                                            t.GetTypeInfo().IsSealed &&
                                            t.Namespace == StartAt.Namespace)
                                         .Select(t => (IState)Activator.CreateInstance(t));*/
            }
        }

        public string Describe(string region, string accountId)
        {


            var sb = new StringBuilder();

            sb.AppendLine("{");
            sb.AppendLine("\"StartAt\": \"" + StartAt.Name + "\",");
            sb.AppendLine("\"States\": {");


            var appendComma = false;
            foreach (var stateType in StateTypes)
            {
                if (appendComma) sb.Append(",");
                DescribeState(sb, stateType, region, accountId);
                appendComma = true;
            }

            sb.Append("}");
            sb.AppendLine("}");

            var rawJson = sb.ToString();

            var anonObject = JsonConvert.DeserializeObject(rawJson);
            var formattedJson = JsonConvert.SerializeObject(anonObject, Formatting.Indented);

            return formattedJson;
        }

        void DescribeState(StringBuilder sb, Type stateType, string region, string accountId)
        {
            sb.AppendLine("\"" + stateType.Name + "\" : { ");

            if (typeof(ITaskState).IsAssignableFrom(stateType))
            {
                sb.AppendLine("\"Type\":\"Task\",");
                sb.AppendLine($"\"Resource\":\"arn:aws:lambda:{region}:{accountId}:function:{GetType().Name}-{stateType.Name}\",");
                sb.AppendLine($"\"Next\":\"{stateType.BaseType.GenericTypeArguments[1].Name}\"");
            }
            if (typeof(IChoiceState).IsAssignableFrom(stateType))
            {
                var choiceState = Activator.CreateInstance(stateType) as IChoiceState;
                sb.AppendLine("\"Type\":\"Choice\",");
                sb.AppendLine("\"Choices\": [");
                var appendComma = false;
                foreach (var choice in choiceState.Choices)
                {
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
                if (choiceState.Default != null)
                {
                    sb.Append(",");
                    sb.AppendLine($"\"Default\": \"{choiceState.Default.Name}\"");
                }
            }
            if (typeof(IPassState).IsAssignableFrom(stateType))
            {
                var passState = Activator.CreateInstance(stateType) as IPassState;
                sb.AppendLine("\"Type\":\"Pass\",");
                if (passState.End)
                    sb.AppendLine("\"End\": true");
            }
            if (typeof(IWaitState).IsAssignableFrom(stateType))
            {
                var waitState = Activator.CreateInstance(stateType) as IWaitState;
                sb.AppendLine("\"Type\":\"Wait\",");
                sb.AppendLine("\"Seconds\": " + waitState.Seconds + ",");
                sb.AppendLine($"\"Next\":\"{waitState.Next.Name}\"");

            }



            sb.AppendLine("}");
        }
    }


}