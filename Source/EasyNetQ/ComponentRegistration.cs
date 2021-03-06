﻿using System;
using EasyNetQ.AutoSubscribe;
using EasyNetQ.Consumer;
using EasyNetQ.Loggers;
using EasyNetQ.Producer;
using EasyNetQ.Rpc;

namespace EasyNetQ
{
    /// <summary>
    /// Registers the default EasyNetQ components in our internal super-simple IoC container.
    /// </summary>
    public class ComponentRegistration
    {
        public static void RegisterServices(IContainer container)
        {
            Preconditions.CheckNotNull(container, "container");

            // Note: IConnectionConfiguration gets registered when RabbitHutch.CreateBus(..) is run.

            // default service registration
            container
                .Register(_ => container)       
                .Register<IEasyNetQLogger, ConsoleLogger>()
                .Register<ISerializer, JsonSerializer>()
                .Register<IConventions, Conventions>()
                .Register<IEventBus, EventBus>()
                .Register<ITypeNameSerializer, TypeNameSerializer>()
                .Register<ICorrelationIdGenerationStrategy, DefaultCorrelationIdGenerationStrategy>()                
                .Register<IMessageSerializationStrategy, DefaultMessageSerializationStrategy>()
                .Register<IMessageDeliveryModeStrategy, MessageDeliveryModeStrategy>()
                .Register<IClusterHostSelectionStrategy<ConnectionFactoryInfo>, DefaultClusterHostSelectionStrategy<ConnectionFactoryInfo>>()
                .Register<IConsumerDispatcherFactory, ConsumerDispatcherFactory>()
                .Register<IPublishExchangeDeclareStrategy, PublishExchangeDeclareStrategy>()
                .Register<IAdvancedPublishExchangeDeclareStrategy, AdvancedPublishExchangeDeclareStrategy>()
                .Register(sp => PublisherFactory.CreatePublisher(sp.Resolve<IConnectionConfiguration>(), sp.Resolve<IEasyNetQLogger>(), sp.Resolve<IEventBus>()))
                .Register<IConsumerErrorStrategy, DefaultConsumerErrorStrategy>()
                .Register<IHandlerRunner, HandlerRunner>()
                .Register<IInternalConsumerFactory, InternalConsumerFactory>()
                .Register<IConsumerFactory, ConsumerFactory>()
                .Register<IConnectionFactory, ConnectionFactoryWrapper>()
                .Register<IPersistentChannelFactory, PersistentChannelFactory>()
                .Register<IClientCommandDispatcherFactory, ClientCommandDispatcherFactory>()
                .Register<IHandlerCollectionFactory, HandlerCollectionFactory>()
                .Register<IConsumeSingleFactory, DefaultConsumeSingleFactory>()
                .Register<IAdvancedBus, RabbitAdvancedBus>()

                .Register<IRpc, Rpc.Rpc>()
                .Register<IRpcHeaderKeys, RpcHeaderKeys>()
                .Register<IAdvancedRpcFactory,AdvancedRpcFactory>()

                .Register<ISendReceive, SendReceive>()
                .Register<IBus, RabbitBus>();
        }
         
    }
}