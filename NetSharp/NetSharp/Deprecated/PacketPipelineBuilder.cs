using System;
using System.Collections.Generic;

namespace NetSharp.Deprecated
{
    /// <summary>
    /// Allows for configuring and subsequently building a <see cref="PacketPipeline{TInput,TIntermediate,TOutput}"/> instance.
    /// </summary>
    /// <typeparam name="TInput">The type of packet that will be submitted to the pipeline.</typeparam>
    /// <typeparam name="TIntermediate">The type of packet that will be handled internally by the pipeline.</typeparam>
    /// <typeparam name="TOutput">The type of packet that will be requested from the pipeline.</typeparam>
    internal sealed class PacketPipelineBuilder<TInput, TIntermediate, TOutput>
    {
        private readonly List<PacketPipelineStage<TIntermediate, TIntermediate>> intermediateStages;

        private PacketPipelineStage<TInput, TIntermediate>? inputStage;
        private PacketPipelineStage<TIntermediate, TOutput>? outputStage;

        internal PacketPipelineBuilder()
        {
            intermediateStages = new List<PacketPipelineStage<TIntermediate, TIntermediate>>();
        }

        /// <summary>
        /// Returns the currently configured <see cref="PacketPipeline{TInput,TIntermediate,TOutput}"/> instance.
        /// </summary>
        /// <returns>The configured <see cref="PacketPipeline{TInput,TIntermediate,TOutput}"/> instance.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when either <see cref="WithInputStage"/> or <see cref="WithOutputStage"/> have not been called.
        /// </exception>
        internal PacketPipeline<TInput, TIntermediate, TOutput> Build()
        {
            if (inputStage == null)
            {
                throw new ArgumentNullException(nameof(inputStage), $"{nameof(WithInputStage)} has not been called.");
            }

            if (outputStage == null)
            {
                throw new ArgumentNullException(nameof(outputStage), $"{nameof(WithOutputStage)} has not been called.");
            }

            return new PacketPipeline<TInput, TIntermediate, TOutput>(inputStage.Value, outputStage.Value, intermediateStages);
        }

        /// <summary>
        /// Configures the input stage for the pipeline.
        /// </summary>
        /// <param name="stage">
        /// The transformation that should be applied to incoming packets, to convert them from the <typeparamref name="TInput"/>
        /// type to the <typeparamref name="TIntermediate"/> type that the pipeline handles internally.
        /// </param>
        /// <returns>The builder instance for further configuration.</returns>
        internal PacketPipelineBuilder<TInput, TIntermediate, TOutput> WithInputStage(in Func<TInput, TIntermediate> stage)
        {
            inputStage = new PacketPipelineStage<TInput, TIntermediate>(in stage);
            return this;
        }

        /// <summary>
        /// Adds the given intermediate stage to the pipeline.
        /// </summary>
        /// <param name="stage">
        /// The transformation that should be applied to packets traveling through the pipeline.
        /// </param>
        /// <returns>The builder instance for further configuration.</returns>
        internal PacketPipelineBuilder<TInput, TIntermediate, TOutput> WithIntermediateStage(in Func<TIntermediate, TIntermediate> stage)
        {
            intermediateStages.Add(new PacketPipelineStage<TIntermediate, TIntermediate>(in stage));
            return this;
        }

        /// <summary>
        /// Configures the output stage for the pipeline.
        /// </summary>
        /// <param name="stage">
        /// The transformation that should be applied to outgoing packets, to convert them from the
        /// <typeparamref name="TIntermediate"/> type used internally to the <typeparamref name="TOutput"/> type.
        /// </param>
        /// <returns>The builder instance for further configuration.</returns>
        internal PacketPipelineBuilder<TInput, TIntermediate, TOutput> WithOutputStage(in Func<TIntermediate, TOutput> stage)
        {
            outputStage = new PacketPipelineStage<TIntermediate, TOutput>(in stage);
            return this;
        }
    }
}