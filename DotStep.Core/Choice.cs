using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace DotStep.Core
{
    public class Choice 
    {
        public string Variable { get; set; } 
        public string Operator { get; set; }
        public object Value { get; set; }
        public Type Next { get; set; }
    }

    public class Choice<TState, TContext> : Choice where TState : IState where TContext : IContext
    {
        public Choice(Expression<Func<TContext, bool>> expression)
        {
            Next = typeof(TState);
            var binaryExpression = expression.Body as BinaryExpression;
            var left = binaryExpression.Left as MemberExpression;
            var property = left.Member as PropertyInfo;
            Variable = property.Name;
            Operator = GetStepFunctionOperator(expression.Body.NodeType, property);
            var right = binaryExpression.Right as ConstantExpression;
            Value = Convert.ToString(right.Value);
        }


        private string GetStepFunctionOperator(ExpressionType nodeType, PropertyInfo property) {

            var typeMapping = new Dictionary<Type, string>
            {
                { typeof(Boolean), "Boolean" },
                {typeof(Int32), "Numeric" },
                {typeof(Int16), "Numeric" },
                {typeof(Int64), "Numeric" },
                { typeof(DateTime), "Timestamp" },
                {typeof(String), "String" }
            };

            var typeMap = typeMapping.Single(map => map.Key == property.PropertyType);

            var operatorMapping = new Dictionary<ExpressionType, string> {
                { ExpressionType.Equal, "Equals"},
                {ExpressionType.GreaterThan, "GreaterThan" },
                { ExpressionType.GreaterThanOrEqual, "GreaterThanEquals"},
                {ExpressionType.LessThan, "LessThan" },
                {ExpressionType.LessThanOrEqual, "LessThanEquals" },
                {ExpressionType.And, "And" },
                {ExpressionType.Or, "Or" },
                {ExpressionType.Not, "Not" }
            };

            var operatorMap = operatorMapping.Single(map => map.Key == nodeType);

            return $"{typeMap.Value}{operatorMap.Value}";


            string stepFunctionOperator = "";

            switch (property.PropertyType.Name) {
                case "Boolean":
                    stepFunctionOperator += "Boolean";
                    break;
                case "Int32":
                case "Int64":
                    stepFunctionOperator += "Numeric";
                    break;
                default:
                    throw new NotImplementedException("Support for property type not implemented: " + property.PropertyType.Name);
            }

            switch (nodeType)
            {
                case ExpressionType.Equal:

                case ExpressionType.GreaterThan:
                    stepFunctionOperator += "GreaterThan";
                    break;
                case ExpressionType.GreaterThanOrEqual:
                    stepFunctionOperator += "GreaterThanEquals";
                    break;
                default:
                    throw new NotImplementedException("nodeType not implemented: " + nodeType);
            }

            return stepFunctionOperator;
        }
    }

    

}
