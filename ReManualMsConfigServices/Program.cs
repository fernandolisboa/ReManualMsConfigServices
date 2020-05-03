//using System.Text.RegularExpressions;
//public static readonly Regex UserRegex = new Regex(@"^.+_\w+$");
//var regexSvcs = AllServices.Where(s => UserRegex.IsMatch(s.ServiceName));
//PrintServices(regexSvcs, "UserRegex");

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.ServiceProcess;
using System.Text;

using static System.StringComparer;

namespace ReManualMsConfigServices
{
    public class Program
    {
        public static string USER_LUID = string.Empty;

        #region props

        #region user

        public static bool UserServicesPredicate(ServiceController s)
            => !string.IsNullOrWhiteSpace(USER_LUID)
            && s.ServiceName.Contains(USER_LUID, StringComparison.OrdinalIgnoreCase);

        public static IEnumerable<string> UserServices = null;

        #endregion user

        public static readonly Dictionary<char, bool> KeyOptions = new Dictionary<char, bool>
        {
            { 's', true },
            { 'S', true },
            { 'n', false },
            { 'N', false },
        };

        public static IEnumerable<ServiceController> AllServices = null;

        public const string AllServicesFilePath = @".\config\AllServices.csv";

        public const string DontTouchServicesFilePath = @".\config\DontTouchServices.txt";
        public static IEnumerable<string> DontTouchServices = null;

        public const string DisabledServicesFilePath = @".\config\DisabledServices.txt";
        public static IEnumerable<string> DisabledServices = null;

        #endregion props
        #region core

        static Program()
        {
            RefreshAllServices();
            RefreshDisabledServices();
        }

        public static void Main()
        {
            HandleConfig();
            WriteAllServicesFile();

            DisableServices();
            ManualServices();

            Console.Write("\n\n\n### Fim da execução. Pressione qualquer tecla para finalizar... ###");
            Console.ReadKey();
        }

        #endregion core
        #region util

        public static bool GetKeyOption(bool defaultOption = false)
        {
            var pressedKey = Console.ReadKey();
            Console.WriteLine("\n");

            if (pressedKey.Key == ConsoleKey.Enter)
            {
                return defaultOption;
            }

            KeyOptions.TryGetValue(pressedKey.KeyChar, out var option);

            return option;
        }

        #endregion util
        #region change services

        public static void ManualServices()
        {
            ChangeServicesStartType(s => !DisabledServices.Contains(s.ServiceName, OrdinalIgnoreCase) && s.StartType == ServiceStartMode.Disabled);
        }

        public static void DisableServices()
        {
            var disableServices = AllServices.Where(s => DisabledServices.Contains(s.ServiceName, OrdinalIgnoreCase) && s.StartType != ServiceStartMode.Disabled);
            ChangeServicesStartType(disableServices, ServiceStartMode.Disabled);

            PrintDisabledServices();
        }

        public static void ChangeServicesStartType(Func<ServiceController, bool> predicate, ServiceStartMode startType = ServiceStartMode.Manual)
        {
            var services = AllServices.Where(predicate);
            ChangeServicesStartType(services, startType);
        }

        public static void ChangeServicesStartType(IEnumerable<ServiceController> services, ServiceStartMode startType = ServiceStartMode.Manual)
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

        public static void ChangeServiceStartType(string serviceName, ServiceStartMode startType = ServiceStartMode.Manual)
        {
            //Console.WriteLine($"Mudando o serviço '{serviceName}' para '{startType}'");

            try
            {
                var sc = new ServiceController(serviceName);

                if (sc.CanStop)
                {
                    sc.Stop();
                }

                var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{sc.ServiceName}", true);
                key.SetValue("Start", (int)startType);

                Console.WriteLine($"Serviço '{serviceName}' alterado para '{startType}'");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Erro mudando o serviço '{serviceName}' para '{startType}'\n\n[[[\n\n{e}\n\n]]]\n");
            }
        }

        #endregion change services
        #region services.msc

        public static void WriteAllServicesFile()
        {
            var orderedServices = AllServices.Select(s => $"{s.DisplayName};{s.ServiceName};{s.StartType};{s.ServiceType};\"{s.GetServiceDescription()}\"")
                                             .OrderBy(s => s, OrdinalIgnoreCase);

            var servicesHeader = new List<string> { "DisplayName;ServiceName;StartType;ServiceType;Description" };
            servicesHeader.AddRange(orderedServices);

            File.WriteAllLines(AllServicesFilePath, servicesHeader, Encoding.UTF8);
        }

        public static void RefreshAllServices() => AllServices = ServiceController.GetServices();

        public static void RefreshUserServices() => UserServices = AllServices.Where(UserServicesPredicate).Select(s => s.ServiceName);

        #endregion services.msc
        #region config

        private static void HandleConfig()
        {
            HandleConfigUserServices();

            RemoveInvalidServices();

            RefreshDontTouchServices();
            RemoveDontTouchServices();
        }

        public static void RefreshDontTouchServices()
        {
            var rawServices = File.ReadAllLines(DontTouchServicesFilePath);
            DontTouchServices = rawServices.Select(s => string.Format(s, USER_LUID));
        }
        public static void RefreshDisabledServices() => DisabledServices = File.ReadAllLines(DisabledServicesFilePath);

        public static void UpdateAndRefreshDisabledServicesFile(IEnumerable<string> newDisabledServices)
        {
            var orderedNewDisabledServices = newDisabledServices.OrderBy(s => s, OrdinalIgnoreCase);

            File.WriteAllLines(DisabledServicesFilePath, orderedNewDisabledServices, Encoding.UTF8);
            RefreshDisabledServices();
        }

        public static void AppendDisabledServices(string service) => AppendDisabledServices(new List<string>() { service });
        public static void AppendDisabledServices(IEnumerable<string> services)
        {
            var newDisabledServices = DisabledServices.Concat(services);
            UpdateAndRefreshDisabledServicesFile(newDisabledServices);
        }

        public static void RemoveDisabledServices(string service) => RemoveDisabledServices(new List<string>() { service });
        public static void RemoveDisabledServices(IEnumerable<string> services)
        {
            //var newDisabledServices = DisabledServices.Where(s => !services.Contains(s, OrdinalIgnoreCase));
            var newDisabledServices = DisabledServices.Except(services, OrdinalIgnoreCase);
            UpdateAndRefreshDisabledServicesFile(newDisabledServices);
        }

        public static void RemoveInvalidServices()
        {
            var allServices = AllServices.Select(s => s.ServiceName);
            var invalidServices = DisabledServices.Except(allServices, OrdinalIgnoreCase);

            RemoveDisabledServices(invalidServices);
        }

        public static void RemoveDontTouchServices()
        {
            var allServices = AllServices.Select(s => s.ServiceName);
            var dontTouchServices = DontTouchServices.Except(allServices, OrdinalIgnoreCase);

            RemoveDisabledServices(dontTouchServices);
        }

        #region user services

        private static void RequestUserLUID()
        {
            Console.Write("Deseja informar o LUID do usuário? [S / n] ");
            var willTypeLUID = GetKeyOption(true);

            if (willTypeLUID)
            {
                while (willTypeLUID && string.IsNullOrWhiteSpace(USER_LUID))
                {
                    Console.Write("LUID (ver em services.msc): ");
                    USER_LUID = Console.ReadLine();
                }
            }
            else
            {
                Console.Write("O não preenchimento do LUID altera os serviços de usuário que estão 'Disabled' para 'Manual'. Tem certeza disso? [s / N]");
                var continueWithoutLUID = GetKeyOption();

                if (!continueWithoutLUID)
                {
                    Console.Clear();
                    RequestUserLUID();
                }
            }

            Console.WriteLine("\n");
        }

        public static void AppendUserServices() => AppendDisabledServices(UserServices);

        private static void HandleConfigUserServices()
        {
            RequestUserLUID();
            RefreshUserServices();
            AppendUserServices();
        }

        #endregion user services

        #endregion config
        #region print services

        public static bool AskToPrint(string tag = "em extenso")
        {
            Console.Write($"\nDeseja imprimir os serviços '{tag}'? [s / N] ");

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

        //public static void PrintService(ServiceController service) => Console.WriteLine($@"{service.DisplayName} ({service.ServiceName}) [{service.Status}]");
        public static void PrintService(ServiceController service) => Console.WriteLine(service.ServiceName);
        public static void PrintServices(IEnumerable<ServiceController> services, string tag = "padrão")
        {
            var printEachService = AskToPrintEachService();

            Console.WriteLine($"Número de serviços '{tag}': {services.Count()}\n");

            if (printEachService)
            {
                Console.WriteLine($"\nINI '{tag}'\n");

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

        public static void PrintUserServices() => PrintServices(UserServicesPredicate, USER_LUID);

        public static void PrintDisabledServices() => PrintServices(s => s.StartType == ServiceStartMode.Disabled, "desabilitados");

        #endregion print services
    }

    public static class Extensions
    {
        public static string GetServiceDescription(this ServiceController service)
        {
            var managementPath = new ManagementPath($"Win32_Service.Name='{service.ServiceName}'");
            using var managementObject = new ManagementObject(managementPath);

            var description = "Sem descrição";
            try
            {
                description = managementObject["Description"]?.ToString();
            }
            catch (Exception ex)
            {
                description += $" [{ex.Message}]";
            }

            return description;
        }
    }
}
