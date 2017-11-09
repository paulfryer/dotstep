# DotStep

DotStep is a framework for creating AWS Step Functions using a code first approach.

## Install the Starter Project

Launch a single click CloudFormation template in one of the supported regions:

us-east-1|us-east-2|us-west-2|eu-west-1|eu-central-1|eu-west-2|ap-southeast-2|ap-northeast-1
---------|---------|---------|---------|------------|---------|--------------|--------------
[![launch stack in us-east-1](cloudformation-launch-stack.png)](https://us-east-1.console.aws.amazon.com/cloudformation/home?region=us-east-1#/stacks/create/review?templateURL=https://s3.amazonaws.com/dotstep-us-east-1/dotstep-starter-template.json&stackName=dotstep-starter)|[![launch stack in us-east-2](cloudformation-launch-stack.png)](https://us-east-2.console.aws.amazon.com/cloudformation/home?region=us-east-2#/stacks/create/review?templateURL=https://s3.amazonaws.com/dotstep-us-east-2/dotstep-starter-template.json&stackName=dotstep-starter)|[![launch stack in us-west-2](cloudformation-launch-stack.png)](https://us-west-2.console.aws.amazon.com/cloudformation/home?region=us-west-2#/stacks/create/review?templateURL=https://s3.amazonaws.com/dotstep-us-west-2/dotstep-starter-template.json&stackName=dotstep-starter)|[![launch stack in eu-west-1](cloudformation-launch-stack.png)](https://eu-west-1.console.aws.amazon.com/cloudformation/home?region=eu-west-1#/stacks/create/review?templateURL=https://s3.amazonaws.com/dotstep-eu-west-1/dotstep-starter-template.json&stackName=dotstep-starter)|[![launch stack in eu-central-1](cloudformation-launch-stack.png)](https://eu-central-1.console.aws.amazon.com/cloudformation/home?region=eu-central-1#/stacks/create/review?templateURL=https://s3.amazonaws.com/dotstep-eu-central-1/dotstep-starter-template.json&stackName=dotstep-starter)|[![launch stack in eu-west-2](cloudformation-launch-stack.png)](https://eu-west-2.console.aws.amazon.com/cloudformation/home?region=eu-west-2#/stacks/create/review?templateURL=https://s3.amazonaws.com/dotstep-eu-west-2/dotstep-starter-template.json&stackName=dotstep-starter)|[![launch stack in ap-southeast-2](cloudformation-launch-stack.png)](https://ap-southeast-2.console.aws.amazon.com/cloudformation/home?region=ap-southeast-2#/stacks/create/review?templateURL=https://s3.amazonaws.com/dotstep-ap-southeast-2/dotstep-starter-template.json&stackName=dotstep-starter)|[![launch stack in ap-northeast-1](cloudformation-launch-stack.png)](https://ap-northeast-1.console.aws.amazon.com/cloudformation/home?region=ap-northeast-1#/stacks/create/review?templateURL=https://s3.amazonaws.com/dotstep-ap-northeast-1/dotstep-starter-template.json&stackName=dotstep-starter)

### CloudFormation Parameters

- _StackName_ - this is the name that will be used for all resources, so keep it small, lowercase and don't use spaces.

- _SourceCodeZIp_ - this is the location of the Ziped source code you want to deploy.

- _SourceCodeDirectory_ - if your Zip'ed source code is not at the Root of the Zip file then you can provide the path within the Zip where the source code starts. Also note your buildspec.yml needs to be in the root directory.


### Built Resources (a CI/CD pipeline for deploying Step Functions)

After launching the CloudFormation template you will notice the following reousrces have been built in your account:
1. A CI/CD Pipeline using CodeCommit, CodeBuild, CodePipeline, and Lambda
2. A populated private git repository in CodeCommit with the initial source code you copied from the CloudFormation Zip parameter.

Wait a few minutes for the CodePipeline to finish building your initial step functions and Lambdas from source code. You watch the CodePipeline while it builds the code and deploys it with a custom "Deployer" Lambda function. This Lambda function will deploy a CloudFormation template that will be the same name as your inital Stack with "-deployment" appended to the end of the Stack name. This "deployment" Stack contains the built Lambda and Step Functions included in your source code. Once this finishes deplying you should see your new Step Functions in your account. 

The CI/CD Pipeline is now in place and will build and deploy your code every time you check into the master branch. You can modify the pipeline to add unit tests, approval steps, etc. You can also delete the sample step functions and start to build your own, at this point the Git repo is now private in your account and can be used for your proprietary projects. 

See this page for info on authenticating with your CodeCommit repository: http://docs.aws.amazon.com/codecommit/latest/userguide/setting-up-gc.html

### Architectual Diagram

![DotStep Architecture](/DotStep.png)

## Development Model

If you deployed the above starter project into your account you can now edit the source code locally. Exploring dependencies you'll notice is there is a reference to a nuget package called [DotStep.Core](https://www.nuget.org/packages/DotStep.Core/) this contains the framework for developing Lambdas and Step Functions locally using .Net Core.

### Code First Approach

This framework is designed to provide an Object Oriented model of [Amazon States Launguage](http://docs.aws.amazon.com/step-functions/latest/dg/concepts-amazon-states-language.html). It represents the main concepts as interfaces you can implement in code. For example a couple key interfaces:

- [_IStateMachine_](/DotStep.Core/IStateMachine.cs) - this represents a "state machine" or "step function". You implement this to start a step function with DotStep.
- [_IState_](/DotStep.Core/IState.cs) - this is the base interface that represents all "states", there are sub interfaces like [ITaskState](/DotStep.Core/ITaskState.cs), and [IChoiceState](/DotStep.Core/IChoiceState.cs) that implement different behaviors.

### Local Development & Debuging

Using this framework you can run and debug your step functions / lambda functions locally in an IDE. You do this by running your state machine through the [StateMachineEngine<TStateMachine, TContext>](/DotStep.Core/StateMachineEngine.cs). This engine emulates the functionality provided by AWS Step Functions, but instead of running the code in the cloud with HTTP calls to Lambda functions, you are running it all locally in process.

### State Types

The following table represents the differnt types of States you can implement in code.

|State Type|Description|Interface|Base Class|
|----------|-----------|---------|----------|
|Task|This represents code that will be executed as a Lambda Function. Everything in the Execute method will be turned into a Lambda Function. You specify the context object TContext and the next state to move to after execution with TNext generic arguments.|ITaskState|TaskState<TContext, TNext>|
|Choice|This represents a choice. You proivde a boolean expression based on the context object used in the state machine which will direct execution flow to the first true statement or default type provided as a generic agument TDefault.|IChoiceState|ChoiceState<TDefault>|
|Wait|This represents an amount of time to wait, you specify this in seconds. You specify the next state to move to after waiting in the generic agurment TNext.|IWaitState|WaitState<TNext>|
|Pass|This simply passes execution to the next state.|IPassState|PassState|


