using System;
using System.Collections.Generic;
using System.Linq;
using Castle.MicroKernel.Registration;
using Castle.MicroKernel.SubSystems.Configuration;
using Castle.Windsor;

namespace EventStoreLite.IoC
{
    /// <summary>
    /// Installs the event store into a Castle Windsor container.
    /// </summary>
    public class EventStoreInstaller : IWindsorInstaller
    {
        private readonly IEnumerable<IEventHandler> handlers;
        private readonly IEnumerable<Type> types;

        public EventStoreInstaller(IEnumerable<Type> types)
        {
            if (types == null) throw new ArgumentNullException("types");
            this.types = types;
        }

        public EventStoreInstaller(IEnumerable<IEventHandler> handlers)
        {
            if (handlers == null) throw new ArgumentNullException("handlers");
            this.handlers = handlers;
        }

        public void Install(IWindsorContainer container, IConfigurationStore store)
        {
            container.Register(
                Component.For<EventStore>()
                         .UsingFactoryMethod<EventStore>(x => CreateEventStore(container))
                         .LifestyleSingleton());

            if (this.types != null)
            {
                foreach (var type in this.types.Where(x => x.IsClass && x.IsAbstract == false))
                {
                    RegisterEventTypes(container, type);
                }
            }

            if (this.handlers != null)
            {
                foreach (var handler in this.handlers)
                {
                    RegisterEventTypes(container, handler.GetType(), handler);
                }
            }
        }

        private EventStore CreateEventStore(IWindsorContainer container)
        {
            if (this.types != null)
                return new EventStore(new WindsorServiceLocator(container)).Initialize(this.types);

            return new EventStore(new WindsorServiceLocator(container)).Initialize(this.handlers.Select(x => x.GetType()));
        }

        private static void RegisterEventTypes(IWindsorContainer container, Type type, object instance = null)
        {
            var interfaces = type.GetInterfaces();
            foreach (var i in interfaces.Where(x => x.IsGenericType))
            {
                var genericTypeDefinition = i.GetGenericTypeDefinition();
                if (!typeof(IEventHandler<>).IsAssignableFrom(genericTypeDefinition)) continue;
                var genericArguments = string.Join(
                    ", ", i.GetGenericArguments().Select(x => x.ToString()));
                var registration =
                    Component.For(i)
                             .Named(string.Format("{0}<{1}>", type.FullName, genericArguments));
                if (instance != null) registration.Instance(instance);
                else
                {
                    registration.ImplementedBy(type).LifestyleTransient();
                }

                container.Register(registration);
            }
        }
    }
}