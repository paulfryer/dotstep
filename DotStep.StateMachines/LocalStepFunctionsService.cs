
using Amazon.Runtime;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using DotStep.Core;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace DotStep.StateMachines
{
    public class LocalStepFunctionsService : IAmazonStepFunctions
    {
        public IClientConfig Config => throw new NotImplementedException();

        public Task<CreateActivityResponse> CreateActivityAsync(CreateActivityRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task<CreateStateMachineResponse> CreateStateMachineAsync(CreateStateMachineRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task<DeleteActivityResponse> DeleteActivityAsync(DeleteActivityRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task<DeleteStateMachineResponse> DeleteStateMachineAsync(DeleteStateMachineRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task<DescribeActivityResponse> DescribeActivityAsync(DescribeActivityRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task<DescribeExecutionResponse> DescribeExecutionAsync(DescribeExecutionRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task<DescribeStateMachineResponse> DescribeStateMachineAsync(DescribeStateMachineRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public Task<GetActivityTaskResponse> GetActivityTaskAsync(GetActivityTaskRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task<GetExecutionHistoryResponse> GetExecutionHistoryAsync(GetExecutionHistoryRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task<ListActivitiesResponse> ListActivitiesAsync(ListActivitiesRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task<ListExecutionsResponse> ListExecutionsAsync(ListExecutionsRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task<ListStateMachinesResponse> ListStateMachinesAsync(ListStateMachinesRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task<SendTaskFailureResponse> SendTaskFailureAsync(SendTaskFailureRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task<SendTaskHeartbeatResponse> SendTaskHeartbeatAsync(SendTaskHeartbeatRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task<SendTaskSuccessResponse> SendTaskSuccessAsync(SendTaskSuccessRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public async Task<StartExecutionResponse> StartExecutionAsync(StartExecutionRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {

            var types = Assembly.GetEntryAssembly().GetTypes();

            var stateMachineType = types
                .Where(type => typeof(IStateMachine).IsAssignableFrom(type) && type.Name == request.Name)
                .FirstOrDefault();

            if (stateMachineType == null)
                throw new Exception($"State Machine not found: {request.Name}");

            var stateMachine = Activator.CreateInstance(stateMachineType) as IStateMachine;

            var context = JsonConvert.DeserializeObject(request.Input, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All});


            throw new NotImplementedException();


            return new StartExecutionResponse
            {
                HttpStatusCode = System.Net.HttpStatusCode.Accepted
            };
        }

        public Task<StopExecutionResponse> StopExecutionAsync(StopExecutionRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }
    }
}
