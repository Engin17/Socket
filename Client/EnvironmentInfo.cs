﻿using log4net;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    public class EnvironmentInfo
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region OS version information

        /// <summary> 
        /// Init OSVersionInfo object by current windows environment 
        /// </summary> 
        /// <returns></returns> 
        public static void GetOSVersionInfo()
        {
            string osVersion = Environment.OSVersion.VersionString;

            log.Info(osVersion);
        }
        #endregion

        #region .NET information
        public static void GetRunningNETRuntimeVersion()
        {
            log.Info("");
            log.Info(".NET Runtime Version: " + Environment.Version.ToString());
        }

        public static void GetInstalledNETVersionFromRegistry()
        {
            log.Info("Detect installed.NET framework versions:");

            try
            {
                // Opens the registry key for the .NET Framework entry.
                using (RegistryKey ndpKey =
                    RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, "").
                    OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\"))
                {
                    // As an alternative, if you know the computers you will query are running .NET Framework 4.5 
                    // or later, you can use:
                    // using (RegistryKey ndpKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, 
                    // RegistryView.Registry32).OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\"))
                    foreach (string versionKeyName in ndpKey.GetSubKeyNames())
                    {
                        if (versionKeyName.StartsWith("v"))
                        {

                            RegistryKey versionKey = ndpKey.OpenSubKey(versionKeyName);
                            string name = (string)versionKey.GetValue("Version", "");
                            string sp = versionKey.GetValue("SP", "").ToString();
                            string install = versionKey.GetValue("Install", "").ToString();
                            if (install == "") //no install info, must be later.
                                log.Info(versionKeyName + "  " + name);
                            else
                            {
                                if (sp != "" && install == "1")
                                {
                                    log.Info(versionKeyName + "  " + name + "  SP" + sp);
                                }

                            }
                            if (name != "")
                            {
                                continue;
                            }
                            foreach (string subKeyName in versionKey.GetSubKeyNames())
                            {
                                RegistryKey subKey = versionKey.OpenSubKey(subKeyName);
                                name = (string)subKey.GetValue("Version", "");
                                if (name != "")
                                    sp = subKey.GetValue("SP", "").ToString();
                                install = subKey.GetValue("Install", "").ToString();
                                if (install == "") //no install info, must be later.
                                    log.Info(versionKeyName + "  " + name);
                                else
                                {
                                    if (sp != "" && install == "1")
                                    {
                                        log.Info("  " + subKeyName + "  " + name + "  SP" + sp);
                                    }
                                    else if (install == "1")
                                    {
                                        log.Info("  " + subKeyName + "  " + name);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
            }

            log.Info("");
        }
        #endregion
    }
}
