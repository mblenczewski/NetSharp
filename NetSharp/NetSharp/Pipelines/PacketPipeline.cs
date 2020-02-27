using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using NetSharp.Packets;

namespace NetSharp.Pipelines
{
    public interface IPacketPipelineStage<TInput, TOutput>
    {
        ChannelReader<TInput> StageInput { get; }

        ChannelWriter<TOutput> StageOutput { get; }

        TOutput Process(TInput input);
    }

    // TODO: Implement a packet pipeline, with multiple transform stages to allow encryption, compression, and various other bytewise manipulation stages.
    public readonly struct PacketPipeline<TInput, TIntermediate, TOutput>
    {
        private readonly IPacketPipelineStage<TIntermediate, TOutput> finalPipelineStage;
        private readonly IPacketPipelineStage<TInput, TIntermediate> initialPipelineStage;
        private readonly IEnumerable<IPacketPipelineStage<TIntermediate, TIntermediate>> intermediatePipelineStages;
        private readonly Channel<TInput> pipelineInput;
        private readonly Channel<TOutput> pipelineOutput;
        private readonly CancellationToken pipelineShutdownToken;

        internal PacketPipeline(CancellationToken shutdownToken,
            IPacketPipelineStage<TInput, TIntermediate> firstStage,
            IPacketPipelineStage<TIntermediate, TOutput> lastStage,
            IEnumerable<IPacketPipelineStage<TIntermediate, TIntermediate>> intermediateStages)
        {
            pipelineShutdownToken = shutdownToken;

            UnboundedChannelOptions inputOptions = new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = true,
            };
            pipelineInput = Channel.CreateUnbounded<TInput>(inputOptions);

            UnboundedChannelOptions outputOptions = new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = true,
            };
            pipelineOutput = Channel.CreateUnbounded<TOutput>(outputOptions);

            initialPipelineStage = firstStage;
            finalPipelineStage = lastStage;

            intermediatePipelineStages = intermediateStages;
            foreach (IPacketPipelineStage<TIntermediate, TIntermediate> stage in intermediatePipelineStages)
            {
            }
        }

        public async Task<TOutput> DequeuePacketAsync()
        {
            return await pipelineOutput.Reader.ReadAsync(pipelineShutdownToken);
        }

        public async Task EnqueuePacketAsync(TInput pipelineInput)
        {
            await this.pipelineInput.Writer.WriteAsync(pipelineInput, pipelineShutdownToken);
        }
    }

    public readonly struct PacketPipelineStage<TInput, TOutput> : IPacketPipelineStage<TInput, TOutput>
    {
        /// <inheritdoc />
        public ChannelReader<TInput> StageInput { get; }

        /// <inheritdoc />
        public ChannelWriter<TOutput> StageOutput { get; }

        /// <inheritdoc />
        public TOutput Process(TInput input)
        {
            throw new NotImplementedException();
        }
    }
}