﻿
namespace Engine
{
    using Engine.Common;

    /// <summary>
    /// Requested resource interface
    /// </summary>
    public interface IGameResourceRequest
    {
        /// <summary>
        /// Engine resource view
        /// </summary>
        EngineShaderResourceView ResourceView { get; }

        /// <summary>
        /// Creates the resource
        /// </summary>
        /// <param name="game">Game</param>
        void Create(Game game);
    }
}