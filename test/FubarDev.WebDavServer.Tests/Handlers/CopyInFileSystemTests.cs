﻿// <copyright file="CopyInFileSystemTests.cs" company="Fubar Development Junker">
// Copyright (c) Fubar Development Junker. All rights reserved.
// </copyright>

namespace FubarDev.WebDavServer.Tests.Handlers
{
    public class CopyInFileSystemTests : CopyTestsBase
    {
        public CopyInFileSystemTests()
            : base(RecursiveProcessingMode.PreferFastest)
        {
        }
    }
}
