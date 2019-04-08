using System.Reflection;
using Grpc.Core;
using System;
using ProtoBuf;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using System.Linq;
using Atlantis.Grpc.Utilies;
using Atlantis.Grpc.Middlewares;
using Atlantis.Grpc.Logging;

namespace Atlantis.Grpc
{
    public class GrpcServer
    {
        private static IList<string> _messages = new List<string>();

        private readonly Server _server;
        private readonly GrpcServerOptions _options;

        public GrpcServer(GrpcServerOptions options)
        {
            _options = options ?? throw new ArgumentNullException("GrpcServerOption cannot be null");
            RegisterThirdParty();
            _server = new Server();
            ObjectContainer.RegisterInstance(new GrpcHandlerBuilder());
            ObjectContainer.Register<ILoggerFactory, LoggerFactory>(LifeScope.Single);
            var grpcCodeClass=GrpcServerBuilder.Instance.GenerateGrpcProxy(_options);
            var proxyCodeClass = GrpcServerBuilder.Instance.GenerateHandlerProxy(_options.ScanAssemblies);
            var codeAssembly = CodeBuilder.Instance.Build();

            var namespaces = $"{proxyCodeClass.Namespace}.{proxyCodeClass.Name}";
            var proxy=(IMessageServicerProxy)codeAssembly.Assembly
                .CreateInstance(namespaces);
            ObjectContainer.RegisterInstance(proxy);

            namespaces=$"{grpcCodeClass.Namespace}.{grpcCodeClass.Name}";
            var grpcType=codeAssembly.Assembly.GetType(namespaces);
            ObjectContainer.Register(typeof(IGrpcServices),grpcType);
        }

        public GrpcServer Start()
        {
            GrpcHandlerDirector.ConfigActor();

            _server.Ports.Add(new ServerPort(
                _options.Host, _options.Port, ServerCredentials.Insecure));
            _server.Services.Add(
                ObjectContainer.Resolve<IGrpcServices>().BindServices());
            _server.Start();

            _messages = null;
            return this;
        }

        public async Task ShutdownAsync(Func<Task> action = null)
        {
            await _server.ShutdownAsync();
            if (action != null)
            {
                await action.Invoke();
            }
        }


        private void RegisterThirdParty()
        {
            ObjectContainer.SetContainer(_options.ObjectContainer);
            ObjectContainer.RegisterInstance(_options.JsonSerializer);
            ObjectContainer.RegisterInstance(_options.BinarySerializer);
        }
    }

    public class ProtoPropertyCompare : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            return string.CompareOrdinal(x, y);
        }
    }
}