﻿using System;
using System.Collections.Generic;

namespace DotStep.Core
{
    public interface IChoiceState
    {

        List<Choice> Choices { get; }

        Type Default { get; }
    }


}
