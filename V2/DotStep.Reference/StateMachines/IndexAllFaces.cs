using System.Collections.Generic;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.S3;
using Amazon.S3.Model;
using DotStep.Core;
using DotStep.Core.StateMachines;
using DotStep.Core.States;
using ResourceNotFoundException = Amazon.Rekognition.Model.ResourceNotFoundException;
using S3Object = Amazon.S3.Model.S3Object;

namespace DotStep.Reference.StateMachines
{
    public class IndexAllFacesRequest
    {
        public string Bucket { get; set; }
        public string Collection { get; set; }
        public string TableName { get; set; }
    }

    public class IndexAllFaces : IStateMachine
    {
        public IState GetStartState()
        {
            var createCollection =
                new AmazonStateTask<IndexAllFacesRequest, AmazonRekognitionClient, CreateCollectionRequest,
                        CreateCollectionResponse>()
                    .SetParameters(i => new CreateCollectionRequest
                        {
                            CollectionId = i.Collection,
                            Tags = new Dictionary<string, string>
                            {
                                { "App", "Test" }
                            }
                        }
                    );

            var waitState = new WaitState();

    

            var getCollection =
                new AmazonStateTask<IndexAllFacesRequest, AmazonRekognitionClient, DescribeCollectionRequest,
                        DescribeCollectionResponse>()
                    .SetParameters(i => new DescribeCollectionRequest
                    {
                        CollectionId = i.Collection
                    })
                    .AddErrorHandler(typeof(ResourceNotFoundException), new ErrorHandler
                    {
                        FallbackState = createCollection
                    });

            var getObject = new AmazonStateTask<GetObjectRequest, AmazonS3Client, GetObjectRequest, GetObjectResponse>()
                .SetParameters(i => i)
                .Catch(typeof(Amazon.DynamoDBv2.Model.ResourceNotFoundException), createCollection)
                .SetNextState(new SuccessState { Name = "Done" });

            var getAllObjects = new MapState<ListObjectsV2Response, S3Object, GetObjectRequest>(getObject)
                .SetIterator(i => i.S3Objects)
                .SetMapping(i => new GetObjectRequest
                {
                    BucketName = i.BucketName,
                    Key = i.Key
                });

            var listObjects =
                new AmazonStateTask<IndexAllFacesRequest, AmazonS3Client, ListObjectsV2Request, ListObjectsV2Response>()
                    .SetParameters(i => new ListObjectsV2Request
                    {
                        BucketName = i.Bucket
                    })
                    .AddErrorHandler(typeof(AmazonS3Exception), new ErrorHandler())
                    .SetNextState(getAllObjects);


            var getItem =
                new AmazonStateTask<IndexAllFacesRequest, AmazonDynamoDBClient, GetItemRequest, GetItemResponse>()
                    .SetParameters(i => new GetItemRequest
                    {
                        TableName = i.TableName,
                        Key = new Dictionary<string, AttributeValue>
                        {
                            { "Partition", new AttributeValue(i.Bucket) }
                        }
                    }).AddErrorHandler(typeof(Amazon.DynamoDBv2.Model.ResourceNotFoundException), new ErrorHandler{FallbackState = new FailState()});


            var passState = new PassState(getItem)
            {

            };

            var choiceState = new ChoiceSate<IndexAllFacesRequest>(passState)
              .AddRule(i => i.Bucket.StartsWith("node") ||
                                                  i.TableName != "Something", waitState)
              .AddRule(i => i.Collection == null, getItem);

            var parallelTask = new ParallelState()
                .AddState(getCollection)
                .AddState(listObjects)
                .AddState(getItem)
                .SetNextState(new SuccessState());

             return choiceState;
            return parallelTask;
        }

        
    }


}    