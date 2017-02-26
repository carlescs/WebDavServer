﻿// <copyright file="InMemoryFile.cs" company="Fubar Development Junker">
// Copyright (c) Fubar Development Junker. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using FubarDev.WebDavServer.Model;
using FubarDev.WebDavServer.Model.Headers;
using FubarDev.WebDavServer.Props.Dead;
using FubarDev.WebDavServer.Props.Live;

namespace FubarDev.WebDavServer.FileSystem.InMemory
{
    /// <summary>
    /// An in-memory implementation of a WebDAV document
    /// </summary>
    public class InMemoryFile : InMemoryEntry, IDocument
    {
        private MemoryStream _data;

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryFile"/> class.
        /// </summary>
        /// <param name="fileSystem">The file system this document belongs to</param>
        /// <param name="parent">The parent collection</param>
        /// <param name="path">The root-relative path of this document</param>
        /// <param name="name">The name of this document</param>
        public InMemoryFile(InMemoryFileSystem fileSystem, InMemoryDirectory parent, Uri path, string name)
            : this(fileSystem, parent, path, name, new byte[0])
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryFile"/> class.
        /// </summary>
        /// <param name="fileSystem">The file system this document belongs to</param>
        /// <param name="parent">The parent collection</param>
        /// <param name="path">The root-relative path of this document</param>
        /// <param name="name">The name of this document</param>
        /// <param name="data">The initial data of this document</param>
        public InMemoryFile(InMemoryFileSystem fileSystem, InMemoryDirectory parent, Uri path, string name, byte[] data)
            : base(fileSystem, parent, path, name)
        {
            _data = new MemoryStream(data);
        }

        /// <inheritdoc />
        public long Length => _data.Length;

        /// <inheritdoc />
        public override async Task<DeleteResult> DeleteAsync(CancellationToken cancellationToken)
        {
            if (InMemoryParent == null)
                throw new InvalidOperationException("The document must belong to a collection");

            if (InMemoryParent.Remove(Name))
            {
                var propStore = FileSystem.PropertyStore;
                if (propStore != null)
                {
                    await propStore.RemoveAsync(this, cancellationToken).ConfigureAwait(false);
                }

                return new DeleteResult(WebDavStatusCode.OK, null);
            }

            return new DeleteResult(WebDavStatusCode.NotFound, this);
        }

        /// <inheritdoc />
        public Task<Stream> OpenReadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<Stream>(new MemoryStream(_data.ToArray()));
        }

        /// <inheritdoc />
        public Task<Stream> CreateAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<Stream>(_data = new MyMemoryStream(this));
        }

        /// <inheritdoc />
        public async Task<IDocument> CopyToAsync(ICollection collection, string name, CancellationToken cancellationToken)
        {
            var coll = (InMemoryDirectory)collection;
            coll.Remove(name);

            var doc = (InMemoryFile)await coll.CreateDocumentAsync(name, cancellationToken).ConfigureAwait(false);
            doc._data = new MemoryStream(_data.ToArray());
            doc.CreationTimeUtc = CreationTimeUtc;
            doc.LastWriteTimeUtc = LastWriteTimeUtc;
            doc.ETag = ETag;

            var sourcePropStore = FileSystem.PropertyStore;
            var destPropStore = collection.FileSystem.PropertyStore;
            if (sourcePropStore != null && destPropStore != null)
            {
                var sourceProps = await sourcePropStore.GetAsync(this, cancellationToken).ConfigureAwait(false);
                await destPropStore.RemoveAsync(doc, cancellationToken).ConfigureAwait(false);
                await destPropStore.SetAsync(doc, sourceProps, cancellationToken).ConfigureAwait(false);
            }
            else if (destPropStore != null)
            {
                await destPropStore.RemoveAsync(doc, cancellationToken).ConfigureAwait(false);
            }

            return doc;
        }

        /// <inheritdoc />
        public async Task<IDocument> MoveToAsync(ICollection collection, string name, CancellationToken cancellationToken)
        {
            var sourcePropStore = FileSystem.PropertyStore;
            var destPropStore = collection.FileSystem.PropertyStore;

            IReadOnlyCollection<XElement> sourceProps;
            if (sourcePropStore != null && destPropStore != null)
            {
                sourceProps = await sourcePropStore.GetAsync(this, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                sourceProps = null;
            }

            var coll = (InMemoryDirectory)collection;
            var doc = (InMemoryFile)await coll.CreateDocumentAsync(name, cancellationToken).ConfigureAwait(false);
            doc._data = new MemoryStream(_data.ToArray());
            doc.CreationTimeUtc = CreationTimeUtc;
            doc.LastWriteTimeUtc = LastWriteTimeUtc;
            doc.ETag = ETag;
            Debug.Assert(InMemoryParent != null, "InMemoryParent != null");
            if (InMemoryParent == null)
                throw new InvalidOperationException("The document must belong to a collection");
            if (!InMemoryParent.Remove(Name))
                throw new InvalidOperationException("Failed to remove the document from the source collection.");

            if (destPropStore != null)
            {
                await destPropStore.RemoveAsync(doc, cancellationToken).ConfigureAwait(false);

                if (sourceProps != null)
                {
                    await destPropStore.SetAsync(doc, sourceProps, cancellationToken).ConfigureAwait(false);
                }
            }

            return doc;
        }

        /// <inheritdoc />
        protected override IEnumerable<ILiveProperty> GetLiveProperties()
        {
            return base.GetLiveProperties()
                       .Concat(new ILiveProperty[]
                       {
                           new ContentLengthProperty(Length),
                       });
        }

        /// <inheritdoc />
        protected override IEnumerable<IDeadProperty> GetPredefinedDeadProperties()
        {
            return base.GetPredefinedDeadProperties()
                .Concat(new[]
                {
                    InMemoryFileSystem.DeadPropertyFactory
                        .Create(FileSystem.PropertyStore, this, GetContentLanguageProperty.PropertyName),
                    InMemoryFileSystem.DeadPropertyFactory
                        .Create(FileSystem.PropertyStore, this, GetContentTypeProperty.PropertyName),
                });
        }

        private class MyMemoryStream : MemoryStream
        {
            private readonly InMemoryFile _file;

            public MyMemoryStream(InMemoryFile file)
            {
                _file = file;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _file._data = new MemoryStream(ToArray());
                    _file.ETag = new EntityTag(false);
                }

                base.Dispose(disposing);
            }
        }
    }
}
