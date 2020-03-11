using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceProcess;

namespace ReManualMsConfigServices
{
    public class Program
    {
        private static bool PrintEachService = false;
        private static readonly Dictionary<char, bool> KeyOptions = new Dictionary<char, bool>
        {
            { 's', true },
            { 'S', true },
            { 'n', false },
            { 'N', false },
        };

        private static ServiceController[] AllServices = null;
        private static readonly IEnumerable<string> SvcsMustStayDisabled = null;

        private static void RefreshAllServices() => AllServices = ServiceController.GetServices();

        static Program()
        {
            SvcsMustStayDisabled = File.ReadAllLines("SvcsMustStayDisabled.txt");

            RefreshAllServices();
        }

        public static void Main()
        {
            ManualServices();

            Console.Write("Fim da execução. Pressione qualquer tecla para finalizar...");
            Console.ReadKey();
        }

        public static void ManualServices()
        {
            var manualServices = AllServices.Where(s => !SvcsMustStayDisabled.Contains(s.ServiceName) && s.StartType == ServiceStartMode.Disabled);
            ChangeServicesStartType(manualServices, ServiceStartMode.Manual);
        }

        public static void DisableServices()
        {
            var disableServices = AllServices.Where(s => SvcsMustStayDisabled.Contains(s.ServiceName) && s.StartType != ServiceStartMode.Disabled);
            ChangeServicesStartType(disableServices, ServiceStartMode.Disabled);

            PrintDisabledServices();
        }

        public static void ChangeServicesStartType(IEnumerable<ServiceController> services, ServiceStartMode startType)
        {
            Console.WriteLine($"{services.Count()} serviços serão alterados para '{startType}'. Deseja continuar? [ s / N ] ");
            var change = GetKeyOption();

            if (change)
            {
                foreach (var service in services)
                {
                    ChangeServiceStartType(service.ServiceName, startType);
                }

                RefreshAllServices();
            }
        }

        public static void ChangeServiceStartType(string serviceName, ServiceStartMode startType)
        {
            //Console.WriteLine($"Mudando o serviço '{serviceName}' para '{startType}'");

            try
            {
                var sc = new ServiceController(serviceName);

                var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{sc.ServiceName}", true);
                key.SetValue("Start", (int)startType);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Erro mudando o serviço '{serviceName}' para '{startType}'\n\n[[[\n\n{e}\n\n]]]\n");
            }

            Console.WriteLine($"Serviço '{serviceName}' alterado para '{startType}'");
        }

        #region util

        public static bool GetKeyOption()
        {
            var pressedKey = Console.ReadKey();
            Console.WriteLine("\n");

            KeyOptions.TryGetValue(pressedKey.KeyChar, out var option);

            return option;
        }

        public static bool AskToPrint(string tag = "em extenso")
        {
            Console.Write($"Deseja imprimir os serviços '{tag}'? [s / N] ");

            var print = GetKeyOption();
            // Console.WriteLine("Opção inválida! Os serviços não serão imprimidos...");

            return print;
        }
        public static void PrintService(ServiceController service) => Console.WriteLine($@"{service.DisplayName} ({service.ServiceName}) [{service.Status}]");
        public static void PrintServices(IEnumerable<ServiceController> services, string tag = "padrão")
        {
            Console.WriteLine($"Número de serviços '{tag}': {services.Count()}\n");

            if (PrintEachService)
            {
                foreach (var service in services)
                {
                    PrintService(service);
                }

                Console.WriteLine($"\nFIM '{tag}'\n");
            }
        }
        public static void Print1d6ab79Services()
        {
            var tag = "1d6ab79";
            var print = AskToPrint(tag);

            if (print)
            {
                var user1d6ab79Services = AllServices.Where(s => s.ServiceName.Contains("1d6ab79"));
                PrintServices(user1d6ab79Services, tag);
            }
        }
        public static void PrintDisabledServices()
        {
            var tag = "desabilitados";
            var print = AskToPrint(tag);

            if (print)
            {
                var disabledServices = AllServices.Where(s => s.StartType == ServiceStartMode.Disabled);
                PrintServices(disabledServices, tag);
            }
        }

        #endregion util
    }
}
