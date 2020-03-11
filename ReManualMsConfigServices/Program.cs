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
        #region props

        private static readonly Dictionary<char, bool> KeyOptions = new Dictionary<char, bool>
        {
            { 's', true },
            { 'S', true },
            { 'n', false },
            { 'N', false },
        };

        private static ServiceController[] AllServices = null;
        private static readonly IEnumerable<string> SvcsMustStayDisabled = null;

        #endregion props

        private static void RefreshAllServices() => AllServices = ServiceController.GetServices();

        static Program()
        {
            SvcsMustStayDisabled = File.ReadAllLines("SvcsMustStayDisabled.txt");

            RefreshAllServices();
        }

        public static void Main()
        {
            //ManualServices();

            Print39b92Services();

            PrintDisabledServices();

            Console.Write("Fim da execução. Pressione qualquer tecla para finalizar...");
            Console.ReadKey();
        }

        #region change services

        public static void ManualServices()
        {
            ChangeServicesStartType(s => !SvcsMustStayDisabled.Contains(s.ServiceName) && s.StartType == ServiceStartMode.Disabled);
        }

        public static void DisableServices()
        {
            var disableServices = AllServices.Where(s => SvcsMustStayDisabled.Contains(s.ServiceName) && s.StartType != ServiceStartMode.Disabled);
            ChangeServicesStartType(disableServices, ServiceStartMode.Disabled);

            PrintDisabledServices();
        }

        public static void ChangeServicesStartType(Func<ServiceController, bool> predicate, ServiceStartMode startType = ServiceStartMode.Manual)
        {
            var services = AllServices.Where(predicate);
            ChangeServicesStartType(services);
        }

        public static void ChangeServicesStartType(IEnumerable<ServiceController> services, ServiceStartMode startType = ServiceStartMode.Manual)
        {
            Console.WriteLine($"{services.Count()} serviços serão alterados para '{startType}'. Deseja continuar? [ s / N ] ");
            var change = GetKeyOption();

            if (change)
            {
                foreach (var service in services)
                {
                    ChangeServiceStartType(service.ServiceName);
                }

                RefreshAllServices();
            }
        }

        public static void ChangeServiceStartType(string serviceName, ServiceStartMode startType = ServiceStartMode.Manual)
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

        #endregion change services
        #region util

        public static bool GetKeyOption()
        {
            var pressedKey = Console.ReadKey();
            Console.WriteLine("\n");

            KeyOptions.TryGetValue(pressedKey.KeyChar, out var option);

            return option;
        }

        #region print services

        public static bool AskToPrint(string tag = "em extenso")
        {
            Console.Write($"Deseja imprimir os serviços '{tag}'? [s / N] ");

            var print = GetKeyOption();
            // Console.WriteLine("Opção inválida! Os serviços não serão imprimidos...");

            return print;
        }
        public static bool AskToPrintEachService()
        {
            Console.Write($"\tDeseja imprimir todos os serviços? [s / N] ");

            var print = GetKeyOption();
            // Console.WriteLine("Opção inválida! Os serviços não serão imprimidos...");

            return print;
        }

        public static void PrintService(ServiceController service) => Console.WriteLine($@"{service.DisplayName} ({service.ServiceName}) [{service.Status}]");
        public static void PrintServices(IEnumerable<ServiceController> services, string tag = "padrão")
        {
            var printEachService = AskToPrintEachService();

            Console.WriteLine($"Número de serviços '{tag}': {services.Count()}\n");

            if (printEachService)
            {
                foreach (var service in services)
                {
                    PrintService(service);
                }

                Console.WriteLine($"\nFIM '{tag}'\n");
            }
        }
        public static void PrintServices(Func<ServiceController, bool> predicate, string tag = "padrão")
        {
            var print = AskToPrint(tag);

            if (print)
            {
                var services = AllServices.Where(predicate);
                PrintServices(services, tag);
            }
        }

        public static void PrintUserServices(string userHash) => PrintServices(s => s.ServiceName.Contains(userHash), userHash);

        public static void Print39b92Services() => PrintUserServices("39b92"); // DELL G3
        public static void Print1d6ab79Services() => PrintUserServices("1d6ab79"); // DESKTOP BSB
        public static void PrintDisabledServices() => PrintServices(s => s.StartType == ServiceStartMode.Disabled, "desabilitados");

        #endregion print services

        #endregion util
    }
}
