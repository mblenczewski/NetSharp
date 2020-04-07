using System;
using System.Collections.Generic;
using System.Linq;

namespace NetSharp.Deprecated
{
    /// <summary>
    /// Represents a pipeline of transformations that packets must undergo.
    /// </summary>
    /// <typeparam name="TInput">The type of packet the pipeline receives.</typeparam>
    /// <typeparam name="TIntermediate">The type of packet the pipeline internally handles.</typeparam>
    /// <typeparam name="TOutput">The type of packet the pipeline outputs.</typeparam>
    // TODO: Implement a packet pipeline, with multiple transform stages to allow encryption, compression, and various other bytewise manipulation stages.
    internal readonly struct PacketPipeline<TInput, TIntermediate, TOutput>
    {
        private readonly PacketPipelineStage<TInput, TIntermediate> pipelineInputStage;
        private readonly IReadOnlyCollection<PacketPipelineStage<TIntermediate, TIntermediate>> pipelineIntermediateStages;
        private readonly PacketPipelineStage<TIntermediate, TOutput> pipelineOutputStage;

        internal PacketPipeline(
            PacketPipelineStage<TInput, TIntermediate> firstStage,
            PacketPipelineStage<TIntermediate, TOutput> lastStage,
            IReadOnlyCollection<PacketPipelineStage<TIntermediate, TIntermediate>> intermediateStages)
        {
            pipelineInputStage = firstStage;
            pipelineOutputStage = lastStage;

            pipelineIntermediateStages = intermediateStages;
        }

        /// <summary>
        /// Passes the given packet through the pipeline.
        /// </summary>
        /// <param name="inputPacket">The incoming packet.</param>
        /// <returns>The outgoing transformed packet.</returns>
        internal TOutput ProcessPacket(TInput inputPacket)
        {
            TIntermediate intermediatePacket = pipelineInputStage.Process(inputPacket);

            intermediatePacket = pipelineIntermediateStages.Aggregate(intermediatePacket, (current, stage) => stage.Process(current));

            return pipelineOutputStage.Process(intermediatePacket);
        }
    }

    /// <summary>
    /// Represents a single transformation applied to a packet traveling through the pipeline.
    /// </summary>
    /// <typeparam name="TInput">The type the transformation takes as input.</typeparam>
    /// <typeparam name="TOutput">The type the transformation produces as output.</typeparam>
    internal readonly struct PacketPipelineStage<TInput, TOutput>
    {
        private readonly Func<TInput, TOutput> stageDelegate;

        internal PacketPipelineStage(in Func<TInput, TOutput> stageProcessingDelegate)
        {
            stageDelegate = stageProcessingDelegate;
        }

        internal TOutput Process(TInput input)
        {
            return stageDelegate(input);
        }
    }
}