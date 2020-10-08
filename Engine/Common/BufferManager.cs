﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Engine.Common
{
    using SharpDX.Direct3D;
    using SharpDX.Direct3D11;
    using SharpDX.DXGI;

    /// <summary>
    /// Buffer manager
    /// </summary>
    public class BufferManager : IDisposable
    {
        /// <summary>
        /// Creates a vertex buffer from IVertexData
        /// </summary>
        /// <param name="graphics">Graphics device</param>
        /// <param name="name">Buffer name</param>
        /// <param name="dynamic">Dynamic or Inmutable buffers</param>
        /// <param name="vertices">Vertices</param>
        /// <returns>Returns new buffer</returns>
        private static Buffer CreateVertexBuffer(Graphics graphics, string name, bool dynamic, IEnumerable<IVertexData> vertices)
        {
            if (vertices?.Any() != true)
            {
                return null;
            }

            return graphics.CreateVertexBuffer(name, vertices, dynamic);
        }
        /// <summary>
        /// Creates an instancing buffer
        /// </summary>
        /// <param name="graphics">Graphics</param>
        /// <param name="name">Buffer name</param>
        /// <param name="dynamic">Dynamic or Inmutable buffers</param>
        /// <param name="instancingData">Instancing data</param>
        /// <returns>Returns the new buffer</returns>
        private static Buffer CreateInstancingBuffer(Graphics graphics, string name, bool dynamic, IEnumerable<VertexInstancingData> instancingData)
        {
            if (instancingData?.Any() != true)
            {
                return null;
            }

            return graphics.CreateVertexBuffer(name, instancingData, dynamic);
        }
        /// <summary>
        /// Creates an index buffer
        /// </summary>
        /// <param name="graphics">Graphics</param>
        /// <param name="name">Buffer name</param>
        /// <param name="dynamic">Dynamic or Inmutable buffers</param>
        /// <param name="indices">Indices</param>
        /// <returns>Returns new buffer</returns>
        private static Buffer CreateIndexBuffer(Graphics graphics, string name, bool dynamic, IEnumerable<uint> indices)
        {
            if (indices?.Any() != true)
            {
                return null;
            }

            return graphics.CreateIndexBuffer(name, indices, dynamic);
        }

        /// <summary>
        /// Game instance
        /// </summary>
        private readonly Game game = null;
        /// <summary>
        /// Reserved slots
        /// </summary>
        private readonly int reservedSlots = 0;
        /// <summary>
        /// Descriptor request list
        /// </summary>
        private readonly List<IBufferDescriptorRequest> requestedDescriptors = new List<IBufferDescriptorRequest>();
        /// <summary>
        /// Vertex buffers
        /// </summary>
        private readonly List<Buffer> vertexBuffers = new List<Buffer>();
        /// <summary>
        /// Vertex buffer bindings
        /// </summary>
        private readonly List<VertexBufferBinding> vertexBufferBindings = new List<VertexBufferBinding>();
        /// <summary>
        /// Index buffer
        /// </summary>
        private readonly List<Buffer> indexBuffers = new List<Buffer>();
        /// <summary>
        /// Vertex buffer descriptors
        /// </summary>
        private readonly List<BufferManagerVertices> vertexBufferDescriptors = new List<BufferManagerVertices>();
        /// <summary>
        /// Instancing buffer descriptors
        /// </summary>
        private readonly List<BufferManagerInstances> instancingBufferDescriptors = new List<BufferManagerInstances>();
        /// <summary>
        /// Index buffer descriptors
        /// </summary>
        private readonly List<BufferManagerIndices> indexBufferDescriptors = new List<BufferManagerIndices>();
        /// <summary>
        /// Input layouts by technique
        /// </summary>
        private readonly Dictionary<EngineEffectTechnique, InputLayout> inputLayouts = new Dictionary<EngineEffectTechnique, InputLayout>();
        /// <summary>
        /// Allocating buffers flag
        /// </summary>
        private bool allocating = false;

        /// <summary>
        /// Gets whether the manager is initialized or not
        /// </summary>
        public bool Initilialized { get; set; } = false;
        /// <summary>
        /// Gets whether any internal buffer descriptor has pending requests
        /// </summary>
        /// <remarks>If not initialized, returns always false</remarks>
        public bool HasPendingRequests
        {
            get
            {
                if (!Initilialized)
                {
                    return false;
                }

                return requestedDescriptors.Count > 0;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="game">Game</param>
        /// <param name="reservedSlots">Reserved slots</param>
        public BufferManager(Game game, int reservedSlots = 1)
        {
            this.game = game;
            this.reservedSlots = reservedSlots;

            for (int i = 0; i < reservedSlots; i++)
            {
                vertexBufferDescriptors.Add(new BufferManagerVertices(VertexTypes.Unknown, true));
            }
        }
        /// <summary>
        /// Destructor
        /// </summary>
        ~BufferManager()
        {
            // Finalizer calls Dispose(false)  
            Dispose(false);
        }
        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        /// <summary>
        /// Dispose resources
        /// </summary>
        /// <param name="disposing">Free managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                allocating = true;

                requestedDescriptors.Clear();

                vertexBufferDescriptors.Clear();
                indexBufferDescriptors.Clear();

                vertexBufferBindings.Clear();

                vertexBuffers.ForEach(b => b?.Dispose());
                vertexBuffers.Clear();

                indexBuffers.ForEach(b => b?.Dispose());
                indexBuffers.Clear();

                inputLayouts.Values.ToList().ForEach(il => il?.Dispose());
                inputLayouts.Clear();
            }
        }

        /// <summary>
        /// Creates and populates vertex, instancing and index buffers
        /// </summary>
        /// <param name="progress">Progress helper</param>
        /// <param name="callback">Callback</param>
        internal void CreateBuffers(IProgress<float> progress, Action callback = null)
        {
            if (allocating)
            {
                return;
            }

            try
            {
                allocating = true;

                if (!Initilialized)
                {
                    Logger.WriteTrace(this, $"Creating reserved buffer descriptors");

                    CreateReservedBuffers();

                    Logger.WriteTrace(this, $"Reserved buffer descriptors created");

                    Initilialized = true;
                }

                if (HasPendingRequests)
                {
                    Logger.WriteTrace(this, $"Processing descriptor requests");

                    //Copy request collection
                    var toAssign = requestedDescriptors
                        .Where(r => r?.Processed == false)
                        .ToArray();

                    float current = 0;

                    DoProcessRequest(progress, ref current, toAssign.Count(), toAssign);

                    Logger.WriteTrace(this, $"Descriptor requests processed");

                    Logger.WriteTrace(this, $"Reallocating buffers");

                    var instancingList = instancingBufferDescriptors
                        .Where(v => v.Dirty)
                        .ToArray();

                    var vertexList = vertexBufferDescriptors
                        .Where(v => v.Dirty)
                        .ToArray();

                    var indexList = indexBufferDescriptors
                        .Where(v => v.Dirty)
                        .ToArray();

                    float total =
                        toAssign.Count() +
                        instancingList.Count() +
                        vertexList.Count() +
                        indexList.Count();

                    ReallocateInstances(progress, ref current, total, instancingList);

                    ReallocateVertexData(progress, ref current, total, vertexList);

                    ReallocateIndexData(progress, ref current, total, indexList);

                    Logger.WriteTrace(this, $"Buffers reallocated");
                }
            }
            catch (Exception ex)
            {
                Logger.WriteError(this, $"Error creating buffers: {ex.Message}", ex);
            }
            finally
            {
                allocating = false;

                callback?.Invoke();
            }
        }
        /// <summary>
        /// Creates reserved buffers
        /// </summary>
        private void CreateReservedBuffers()
        {
            for (int i = 0; i < reservedSlots; i++)
            {
                var descriptor = vertexBufferDescriptors[i];

                int bufferIndex = vertexBuffers.Count;
                int bindingIndex = vertexBufferBindings.Count;

                string name = $"Reserved buffer.{bufferIndex}.{(descriptor.Dynamic ? "dynamic" : "static")}";

                //Empty buffer
                vertexBuffers.Add(null);
                vertexBufferBindings.Add(new VertexBufferBinding());

                descriptor.ClearInputs();

                descriptor.BufferIndex = bufferIndex;
                descriptor.BufferBindingIndex = bindingIndex;
                descriptor.AllocatedSize = descriptor.Data.Count();
                descriptor.Allocated = true;
                descriptor.ReallocationNeeded = false;

                Logger.WriteTrace(this, $"Created {name} and binding. Size {descriptor.Data.Count()}");
            }
        }
        /// <summary>
        /// Do descriptor request processing
        /// </summary>
        private void DoProcessRequest(IProgress<float> progress, ref float current, float total, IEnumerable<IBufferDescriptorRequest> toAssign)
        {
            foreach (var request in toAssign)
            {
                request.Process(this);

                progress?.Report(++current / total);
            }

            Monitor.Enter(requestedDescriptors);
            requestedDescriptors.RemoveAll(r => r?.Processed != false);
            Monitor.Exit(requestedDescriptors);
        }
        /// <summary>
        /// Reallocates the instance data
        /// </summary>
        private void ReallocateInstances(IProgress<float> progress, ref float current, float total, IEnumerable<BufferManagerInstances> dirtyList)
        {
            foreach (var descriptor in dirtyList)
            {
                if (descriptor.Allocated)
                {
                    //Reserve current buffer
                    var oldBuffer = vertexBuffers[descriptor.BufferIndex];

                    //Recreate the buffer and binding
                    string name = $"InstancingBuffer.{descriptor.BufferIndex}.{(descriptor.Dynamic ? "dynamic" : "static")}";
                    VertexInstancingData[] data = new VertexInstancingData[descriptor.Instances];
                    var buffer = CreateInstancingBuffer(game.Graphics, name, descriptor.Dynamic, data);
                    var binding = new VertexBufferBinding(buffer, data[0].GetStride(), 0);

                    vertexBuffers[descriptor.BufferIndex] = buffer;
                    vertexBufferBindings[descriptor.BufferBindingIndex] = binding;

                    //Dispose old buffer
                    oldBuffer?.Dispose();

                    Logger.WriteTrace(this, $"Reallocated {name}. Size {descriptor.Instances}");
                }
                else
                {
                    int bufferIndex = vertexBuffers.Count;
                    int bindingIndex = vertexBufferBindings.Count;

                    //Create the buffer and binding
                    string name = $"InstancingBuffer.{bufferIndex}.{(descriptor.Dynamic ? "dynamic" : "static")}";
                    VertexInstancingData[] data = new VertexInstancingData[descriptor.Instances];
                    var buffer = CreateInstancingBuffer(game.Graphics, name, descriptor.Dynamic, data);
                    var binding = new VertexBufferBinding(buffer, data[0].GetStride(), 0);

                    vertexBuffers.Add(buffer);
                    vertexBufferBindings.Add(binding);

                    descriptor.BufferIndex = bufferIndex;
                    descriptor.BufferBindingIndex = bindingIndex;

                    Logger.WriteTrace(this, $"Created {name} and binding. Size {descriptor.Instances}");
                }

                //Updates the allocated buffer size
                descriptor.AllocatedSize = descriptor.Instances;
                descriptor.Allocated = true;
                descriptor.ReallocationNeeded = false;

                progress?.Report(++current / total);
            }
        }
        /// <summary>
        /// Reallocates the vertex data
        /// </summary>
        /// <param name="reallocateInstances">Returns wether instance reallocation is necessary</param>
        private void ReallocateVertexData(IProgress<float> progress, ref float current, float total, IEnumerable<BufferManagerVertices> dirtyList)
        {
            foreach (var descriptor in dirtyList)
            {
                if (descriptor.Allocated)
                {
                    //Reserve current buffer
                    var oldBuffer = vertexBuffers[descriptor.BufferIndex];

                    //Recreate the buffer and binding
                    string name = $"VertexBuffer.{descriptor.BufferIndex}.{(descriptor.Dynamic ? "dynamic" : "static")}";
                    var buffer = CreateVertexBuffer(game.Graphics, name, descriptor.Dynamic, descriptor.Data);
                    var binding = new VertexBufferBinding(buffer, descriptor.GetStride(), 0);

                    vertexBuffers[descriptor.BufferIndex] = buffer;
                    vertexBufferBindings[descriptor.BufferBindingIndex] = binding;

                    //Dispose old buffer
                    oldBuffer?.Dispose();

                    Logger.WriteTrace(this, $"Reallocated {name} and binding. Size {descriptor.Data.Count()}");
                }
                else
                {
                    int bufferIndex = vertexBuffers.Count;
                    int bindingIndex = vertexBufferBindings.Count;

                    //Create the buffer and binding
                    string name = $"VertexBuffer.{bufferIndex}.{(descriptor.Dynamic ? "dynamic" : "static")}";
                    var buffer = CreateVertexBuffer(game.Graphics, name, descriptor.Dynamic, descriptor.Data);
                    var binding = new VertexBufferBinding(buffer, descriptor.GetStride(), 0);

                    vertexBuffers.Add(buffer);
                    vertexBufferBindings.Add(binding);

                    descriptor.AddInputs(bufferIndex);

                    descriptor.BufferIndex = bufferIndex;
                    descriptor.BufferBindingIndex = bindingIndex;

                    Logger.WriteTrace(this, $"Created {name} and binding. Size {descriptor.Data.Count()}");
                }

                descriptor.ClearInstancingInputs();

                if (descriptor.InstancingDescriptor != null)
                {
                    var bufferIndex = instancingBufferDescriptors[descriptor.InstancingDescriptor.BufferDescriptionIndex].BufferIndex;
                    descriptor.AddInstancingInputs(bufferIndex);
                }

                //Updates the allocated buffer size
                descriptor.AllocatedSize = descriptor.Data.Count();
                descriptor.Allocated = true;
                descriptor.ReallocationNeeded = false;

                progress?.Report(++current / total);
            }
        }
        /// <summary>
        /// Reallocates the index data
        /// </summary>
        private void ReallocateIndexData(IProgress<float> progress, ref float current, float total, IEnumerable<BufferManagerIndices> dirtyList)
        {
            foreach (var descriptor in dirtyList)
            {
                if (descriptor.Allocated)
                {
                    //Recreate the buffer
                    string name = $"IndexBuffer.{descriptor.BufferIndex}.{(descriptor.Dynamic ? "dynamic" : "static")}";
                    var buffer = CreateIndexBuffer(game.Graphics, name, descriptor.Dynamic, descriptor.Data);

                    //Reserve current buffer
                    var oldBuffer = indexBuffers[descriptor.BufferIndex];
                    //Replace buffer
                    indexBuffers[descriptor.BufferIndex] = buffer;
                    //Dispose buffer
                    oldBuffer?.Dispose();

                    Logger.WriteTrace(this, $"Reallocated {name}. Size {descriptor.Data.Count()}");
                }
                else
                {
                    int bufferIndex = indexBuffers.Count;

                    //Recreate the buffer
                    string name = $"IndexBuffer.{bufferIndex}.{(descriptor.Dynamic ? "dynamic" : "static")}";
                    var buffer = CreateIndexBuffer(game.Graphics, name, descriptor.Dynamic, descriptor.Data);

                    indexBuffers.Add(buffer);

                    descriptor.BufferIndex = bufferIndex;

                    Logger.WriteTrace(this, $"Created {name}. Size {descriptor.Data.Count()}");
                }

                //Updates the allocated buffer size
                descriptor.AllocatedSize = descriptor.Data.Count();
                descriptor.Allocated = true;
                descriptor.ReallocationNeeded = false;

                progress?.Report(++current / total);
            }
        }

        /// <summary>
        /// Adds vertices to manager
        /// </summary>
        /// <typeparam name="T">Type of vertex</typeparam>
        /// <param name="id">Id</param>
        /// <param name="dynamic">Add to dynamic buffers</param>
        /// <param name="data">Vertex list</param>
        /// <param name="instancingBuffer">Instancing buffer descriptor</param>
        public BufferDescriptor AddVertexData<T>(string id, bool dynamic, IEnumerable<T> data, BufferDescriptor instancingBuffer = null) where T : struct, IVertexData
        {
            return AddVertexData(
                id,
                dynamic,
                data.OfType<IVertexData>(),
                instancingBuffer);
        }
        /// <summary>
        /// Adds vertices to manager
        /// </summary>
        /// <param name="id">Id</param>
        /// <param name="dynamic">Add to dynamic buffers</param>
        /// <param name="data">Vertex list</param>
        /// <param name="instancingBuffer">Instancing buffer descriptor</param>
        public BufferDescriptor AddVertexData(string id, bool dynamic, IEnumerable<IVertexData> data, BufferDescriptor instancingBuffer = null)
        {
            BufferDescriptorRequestVertices request = new BufferDescriptorRequestVertices
            {
                Id = id,
                Data = data,
                Dynamic = dynamic,
                InstancingDescriptor = instancingBuffer,
                Action = BufferDescriptorRequestActions.Add,
            };

            requestedDescriptors.Add(request);

            return request.VertexDescriptor;
        }
        /// <summary>
        /// Adds instances to manager
        /// </summary>
        /// <param name="id">Id</param>
        /// <param name="dynamic">Add to dynamic buffers</param>
        /// <param name="instances">Number of instances</param>
        public BufferDescriptor AddInstancingData(string id, bool dynamic, int instances)
        {
            BufferDescriptorRequestInstancing request = new BufferDescriptorRequestInstancing
            {
                Id = id,
                Dynamic = dynamic,
                Instances = instances,
                Action = BufferDescriptorRequestActions.Add,
            };

            requestedDescriptors.Add(request);

            return request.Descriptor;
        }
        /// <summary>
        /// Adds indices to manager
        /// </summary>
        /// <param name="id">Id</param>
        /// <param name="dynamic">Add to dynamic buffers</param>
        /// <param name="data">Index list</param>
        public BufferDescriptor AddIndexData(string id, bool dynamic, IEnumerable<uint> data)
        {
            BufferDescriptorRequestIndices request = new BufferDescriptorRequestIndices
            {
                Id = id,
                Data = data,
                Dynamic = dynamic,
                Action = BufferDescriptorRequestActions.Add,
            };

            requestedDescriptors.Add(request);

            return request.Descriptor;
        }

        /// <summary>
        /// Removes vertex data from buffer manager
        /// </summary>
        /// <param name="descriptor">Buffer descriptor</param>
        public void RemoveVertexData(BufferDescriptor descriptor)
        {
            BufferDescriptorRequestVertices request = new BufferDescriptorRequestVertices
            {
                Id = descriptor.Id,
                VertexDescriptor = descriptor,
                Action = BufferDescriptorRequestActions.Remove,
            };

            requestedDescriptors.Add(request);
        }
        /// <summary>
        /// Removes instancing data from buffer manager
        /// </summary>
        /// <param name="descriptor">Buffer descriptor</param>
        public void RemoveInstancingData(BufferDescriptor descriptor)
        {
            BufferDescriptorRequestInstancing request = new BufferDescriptorRequestInstancing
            {
                Id = descriptor.Id,
                Descriptor = descriptor,
                Action = BufferDescriptorRequestActions.Remove,
            };

            requestedDescriptors.Add(request);
        }
        /// <summary>
        /// Removes index data from buffer manager
        /// </summary>
        /// <param name="descriptor">Buffer descriptor</param>
        public void RemoveIndexData(BufferDescriptor descriptor)
        {
            BufferDescriptorRequestIndices request = new BufferDescriptorRequestIndices
            {
                Id = descriptor.Id,
                Descriptor = descriptor,
                Action = BufferDescriptorRequestActions.Remove,
            };

            requestedDescriptors.Add(request);
        }

        /// <summary>
        /// Adds a new vertex buffer description into the buffer manager
        /// </summary>
        /// <param name="description">Vertex buffer description</param>
        /// <returns>Returns the internal description index</returns>
        internal int AddVertexBufferDescription(BufferManagerVertices description)
        {
            int index = vertexBufferDescriptors.Count;

            vertexBufferDescriptors.Add(description);

            return index;
        }
        /// <summary>
        /// Finds a vertex buffer description in the buffer manager
        /// </summary>
        /// <param name="vertexType">Vertex type</param>
        /// <param name="dynamic">Dynamic</param>
        /// <returns>Returns the internal description index</returns>
        internal int FindVertexBufferDescription(VertexTypes vertexType, bool dynamic)
        {
            return vertexBufferDescriptors.FindIndex(k =>
                k.Type == vertexType &&
                k.Dynamic == dynamic);
        }
        /// <summary>
        /// Gets the vertex buffer description in the specified index
        /// </summary>
        /// <param name="index">Index</param>
        /// <returns>Returns the description</returns>
        internal BufferManagerVertices GetVertexBufferDescription(int index)
        {
            return vertexBufferDescriptors[index];
        }
        /// <summary>
        /// Adds a new instancing buffer description into the buffer manager
        /// </summary>
        /// <param name="description">Instancing buffer description</param>
        /// <returns>Returns the internal description index</returns>
        internal int AddInstancingBufferDescription(BufferManagerInstances description)
        {
            int index = instancingBufferDescriptors.Count;

            instancingBufferDescriptors.Add(description);

            return index;
        }
        /// <summary>
        /// Finds a instancing buffer description in the buffer manager
        /// </summary>
        /// <param name="dynamic">Dynamic</param>
        /// <returns>Returns the internal description index</returns>
        internal int FindInstancingBufferDescription(bool dynamic)
        {
            return instancingBufferDescriptors.FindIndex(k => k.Dynamic == dynamic);
        }
        /// <summary>
        /// Gets the instancing buffer description in the specified index
        /// </summary>
        /// <param name="index">Index</param>
        /// <returns>Returns the description</returns>
        internal BufferManagerInstances GetInstancingBufferDescription(int index)
        {
            return instancingBufferDescriptors[index];
        }
        /// <summary>
        /// Adds a new index buffer description into the buffer manager
        /// </summary>
        /// <param name="description">Index buffer description</param>
        /// <returns>Returns the internal description index</returns>
        internal int AddIndexBufferDescription(BufferManagerIndices description)
        {
            int index = indexBufferDescriptors.Count;

            indexBufferDescriptors.Add(description);

            return index;
        }
        /// <summary>
        /// Finds a index buffer description in the buffer manager
        /// </summary>
        /// <param name="dynamic">Dynamic</param>
        /// <returns>Returns the internal description index</returns>
        internal int FindIndexBufferDescription(bool dynamic)
        {
            return indexBufferDescriptors.FindIndex(k => k.Dynamic == dynamic);
        }
        /// <summary>
        /// Gets the index buffer description in the specified index
        /// </summary>
        /// <param name="index">Index</param>
        /// <returns>Returns the description</returns>
        internal BufferManagerIndices GetIndexBufferDescription(int index)
        {
            return indexBufferDescriptors[index];
        }

        /// <summary>
        /// Sets vertex buffers to device context
        /// </summary>
        public bool SetVertexBuffers()
        {
            if (!Initilialized)
            {
                Logger.WriteWarning(this, "Attempt to set vertex buffers to Input Assembler with no initialized manager");
                return false;
            }

            if (vertexBufferDescriptors.Any(d => d.Dirty))
            {
                return false;
            }

            game.Graphics.IASetVertexBuffers(0, vertexBufferBindings.ToArray());

            return true;
        }
        /// <summary>
        /// Sets index buffers to device context
        /// </summary>
        /// <param name="descriptor">Buffer descriptor</param>
        public bool SetIndexBuffer(BufferDescriptor descriptor)
        {
            if (descriptor?.Ready != true)
            {
                return false;
            }

            if (!Initilialized)
            {
                Logger.WriteWarning(this, "Attempt to set index buffers to Input Assembler with no initialized manager");
                return false;
            }

            var indexBufferDescriptor = indexBufferDescriptors[descriptor.BufferDescriptionIndex];
            if (indexBufferDescriptor.Dirty)
            {
                Logger.WriteWarning(this, $"Attempt to set index buffer in buffer description {descriptor.BufferDescriptionIndex} to Input Assembler with no allocated buffer");
                return false;
            }

            game.Graphics.IASetIndexBuffer(indexBuffers[descriptor.BufferDescriptionIndex], Format.R32_UInt, 0);
            return true;
        }
        /// <summary>
        /// Sets input layout to device context
        /// </summary>
        /// <param name="technique">Technique</param>
        /// <param name="descriptor">Buffer descriptor</param>
        /// <param name="topology">Topology</param>
        public bool SetInputAssembler(EngineEffectTechnique technique, BufferDescriptor descriptor, Topology topology)
        {
            if (descriptor?.Ready != true)
            {
                return false;
            }

            if (!Initilialized)
            {
                Logger.WriteWarning(this, "Attempt to set technique to Input Assembler with no initialized manager");
                return false;
            }

            var vertexBufferDescriptor = vertexBufferDescriptors[descriptor.BufferDescriptionIndex];
            if (vertexBufferDescriptor.Dirty)
            {
                Logger.WriteWarning(this, $"Attempt to set technique in buffer description {descriptor.BufferDescriptionIndex} to Input Assembler with no allocated buffer");
                return false;
            }

            //The technique defines the vertex type
            if (!inputLayouts.ContainsKey(technique))
            {
                var signature = technique.GetSignature();

                inputLayouts.Add(
                    technique,
                    game.Graphics.CreateInputLayout(signature, vertexBufferDescriptor.Input.ToArray()));
            }

            game.Graphics.IAInputLayout = inputLayouts[technique];
            game.Graphics.IAPrimitiveTopology = (PrimitiveTopology)topology;
            return true;
        }

        /// <summary>
        /// Writes vertex data into buffer
        /// </summary>
        /// <typeparam name="T">Data type</typeparam>
        /// <param name="descriptor">Buffer descriptor</param>
        /// <param name="data">Data to write</param>
        public bool WriteVertexBuffer<T>(BufferDescriptor descriptor, IEnumerable<T> data)
            where T : struct
        {
            if (descriptor?.Ready != true)
            {
                return false;
            }

            if (!Initilialized)
            {
                Logger.WriteWarning(this, $"Attempt to write vertex data in buffer description {descriptor.BufferDescriptionIndex} with no initialized manager");
                return false;
            }

            var vertexBufferDescriptor = vertexBufferDescriptors[descriptor.BufferDescriptionIndex];
            if (vertexBufferDescriptor.Dirty)
            {
                Logger.WriteWarning(this, $"Attempt to write vertex data in buffer description {descriptor.BufferDescriptionIndex} with no allocated buffer");
                return false;
            }

            if (data?.Any() == true)
            {
                var buffer = vertexBuffers[vertexBufferDescriptor.BufferIndex];

                game.Graphics.WriteNoOverwriteBuffer(buffer, descriptor.BufferOffset, data);
            }

            return true;
        }
        /// <summary>
        /// Writes instancing data
        /// </summary>
        /// <param name="descriptor">Buffer descriptor</param>
        /// <param name="data">Instancig data</param>
        public bool WriteInstancingData<T>(BufferDescriptor descriptor, IEnumerable<T> data)
            where T : struct
        {
            if (descriptor?.Ready != true)
            {
                return false;
            }

            if (!Initilialized)
            {
                Logger.WriteWarning(this, "Attempt to write instancing data with no initialized manager");
                return false;
            }

            var instancingBufferDescriptor = instancingBufferDescriptors[descriptor.BufferDescriptionIndex];
            if (instancingBufferDescriptor.Dirty)
            {
                Logger.WriteWarning(this, $"Attempt to write instancing data in buffer description {descriptor.BufferDescriptionIndex} with no allocated buffer");
                return false;
            }

            if (data?.Any() == true)
            {
                var instancingBuffer = vertexBuffers[instancingBufferDescriptor.BufferIndex];
                if (instancingBuffer != null)
                {
                    game.Graphics.WriteDiscardBuffer(instancingBuffer, descriptor.BufferOffset, data);
                }
            }

            return true;
        }
        /// <summary>
        /// Writes imdex data into buffer
        /// </summary>
        /// <param name="descriptor">Buffer descriptor</param>
        /// <param name="data">Data to write</param>
        public bool WriteIndexBuffer(BufferDescriptor descriptor, IEnumerable<uint> data)
        {
            if (descriptor?.Ready != true)
            {
                return false;
            }

            if (!Initilialized)
            {
                Logger.WriteWarning(this, $"Attempt to write index data in buffer description {descriptor.BufferDescriptionIndex} with no initialized manager");
                return false;
            }

            var indexBufferDescriptor = indexBufferDescriptors[descriptor.BufferDescriptionIndex];
            if (indexBufferDescriptor.Dirty)
            {
                Logger.WriteWarning(this, $"Attempt to write index data in buffer description {descriptor.BufferDescriptionIndex} with no allocated buffer");
                return false;
            }

            if (data?.Any() == true)
            {
                var buffer = indexBuffers[indexBufferDescriptor.BufferIndex];

                game.Graphics.WriteNoOverwriteBuffer(buffer, descriptor.BufferOffset, data);
            }

            return true;
        }
    }
}
