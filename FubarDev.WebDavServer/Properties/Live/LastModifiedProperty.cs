﻿using System;
using System.Xml.Linq;

using FubarDev.WebDavServer.Model;
using FubarDev.WebDavServer.Properties.Generic;

namespace FubarDev.WebDavServer.Properties.Live
{
    public class LastModifiedProperty : GenericDateTimeRfc1123Property, ILiveProperty
    {
        public static readonly XName PropertyName = WebDavXml.Dav + "getlastmodified";

        public LastModifiedProperty(GetPropertyValueAsyncDelegate<DateTime> getPropertyValueAsync, SetPropertyValueAsyncDelegate<DateTime> setValueAsyncFunc)
            : base(PropertyName, 0, getPropertyValueAsync, setValueAsyncFunc)
        {
        }
    }
}