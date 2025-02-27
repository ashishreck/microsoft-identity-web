﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Identity.Web.Test.Common;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace WebAppUiTests
{
    public static class UiTestHelpers
    {
        /// <summary>
        /// Login flow for the first time in a given browsing session.
        /// </summary>
        /// <param name="page">Playwright Page object the web app is accessed from</param>
        /// <param name="email">email of the user to sign in</param>
        /// <param name="password">password for sign in</param>
        /// <param name="output">Used to communicate output to the test's Standard Output</param>
        /// <param name="staySignedIn">Whether to select "stay signed in" on login</param>
        public static async Task FirstLogin_MicrosoftIdFlow_ValidEmailPassword(IPage page, string email, string password, ITestOutputHelper? output = null, bool staySignedIn = false)
        {
            string staySignedInText = staySignedIn ? "Yes" : "No";

            WriteLine(output, $"Logging in ... Entering and submitting user name: {email}.");
            ILocator emailInputLocator = page.GetByPlaceholder(TestConstants.EmailText);
            await FillEntryBox(emailInputLocator, email);
            await EnterPassword_MicrosoftIdFlow_ValidPassword(page, password, staySignedInText);
        }

        /// <summary>
        /// Login flow for anytime after the first time in a given browsing session.
        /// </summary>
        /// <param name="page">Playwright Page object the web app is accessed from</param>
        /// <param name="email">email of the user to sign in</param>
        /// <param name="password">password for sign in</param>
        /// <param name="output">Used to communicate output to the test's Standard Output</param>
        /// <param name="staySignedIn">Whether to select "stay signed in" on login</param>
        public static async Task SuccessiveLogin_MicrosoftIdFlow_ValidEmailPassword(IPage page, string email, string password, ITestOutputHelper? output = null, bool staySignedIn = false)
        {
            string staySignedInText = staySignedIn ? "Yes" : "No";

            WriteLine(output, $"Logging in again in this browsing session... selecting user via email: {email}.");
            await SelectKnownAccountByEmail_MicrosoftIdFlow(page, email);
            await EnterPassword_MicrosoftIdFlow_ValidPassword(page, password, staySignedInText);
        }

        /// <summary>
        /// Signs the current user out of the web app.
        /// </summary>
        /// <param name="page">Playwright Page object the web app is accessed from</param>
        /// <param name="email">email of the user to sign out</param>
        /// <param name="signOutPageUrl">The url for the page arrived at once successfully signed out</param>
        public static async Task PerformSignOut_MicrosoftIdFlow(IPage page, string email, string signOutPageUrl, ITestOutputHelper? output = null)
        {
            WriteLine(output, "Signing out ...");
            await SelectKnownAccountByEmail_MicrosoftIdFlow(page, email);
            await page.WaitForURLAsync(signOutPageUrl);
            WriteLine(output, "Sign out page successfully reached.");
        }

        /// <summary>
        /// In the Microsoft Identity flow, the user is at certain stages presented with a list of accounts known in 
        /// the current browsing session to choose from. This method selects the account using the user's email.
        /// </summary>
        /// <param name="page">page for the playwright browser</param>
        /// <param name="email">user email address to select</param>
        private static async Task SelectKnownAccountByEmail_MicrosoftIdFlow(IPage page, string email)
        {
            await page.Locator($"[data-test-id=\"{email}\"]").ClickAsync();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="page"></param>
        /// <param name="password"></param>
        /// <param name="staySignedInText"></param>
        /// <param name="output"></param>
        public static async Task EnterPassword_MicrosoftIdFlow_ValidPassword(IPage page, string password, string staySignedInText, ITestOutputHelper? output = null)
        {
            // If using an account that has other non-password validation options, the below code should be uncommented
            /* WriteLine(output, "Selecting \"Password\" as authentication method"); 
            await page.GetByRole(AriaRole.Button, new() { Name = TestConstants.PasswordText }).ClickAsync();*/

            WriteLine(output, "Logging in ... entering and submitting password.");
            ILocator passwordInputLocator = page.GetByPlaceholder(TestConstants.PasswordText);
            await FillEntryBox(passwordInputLocator, password);

            WriteLine(output, $"Logging in ... Clicking {staySignedInText} on whether the browser should stay signed in.");
            await page.GetByRole(AriaRole.Button, new() { Name = staySignedInText }).ClickAsync();
        }

        public static async Task FillEntryBox(ILocator entryBox, string entryText)
        {
            await entryBox.ClickAsync();
            await entryBox.FillAsync(entryText);
            await entryBox.PressAsync("Enter");
        }
        private static void WriteLine(ITestOutputHelper? output, string message)
        {
            if (output != null)
            {
                output.WriteLine(message);
            }
            else
            {
                Trace.WriteLine(message);
            }
        }

        /// <summary>
        /// This starts the recording of playwright trace files. The corresponsing EndAndWritePlaywrightTrace method will also need to be used.
        /// This is not used anywhere by default and will need to be added to the code if desired
        /// </summary>
        /// <param name="page">The page object whose context the trace will record</param>
        public static async Task StartPlaywrightTrace(IPage page)
        {
            await page.Context.Tracing.StartAsync(new()
            {
                Screenshots = true,
                Snapshots = true,
                Sources = true
            });
        }

        /// <summary>
        /// Starts a process from an executable, sets its working directory, and redirects its output to the test's output
        /// </summary>
        /// <param name="testAssemblyLocation">The path to the test's directory</param>
        /// <param name="appLocation">The path to the processes directory</param>
        /// <param name="executableName">The name of the executable that launches the process</param>
        /// <param name="portNumber">The port for the process to listen on</param>
        /// <param name="isHttp">If the launch URL is http or https. Default is https.</param>
        /// <returns>The started process</returns>
        public static Process? StartProcessLocally(string testAssemblyLocation, string appLocation, string executableName, uint? portNumber = null, bool isHttp = false)
        {
            string applicationWorkingDirectory = GetApplicationWorkingDirectory(testAssemblyLocation, appLocation);
            ProcessStartInfo processStartInfo = new ProcessStartInfo(applicationWorkingDirectory + executableName);
            processStartInfo.WorkingDirectory = applicationWorkingDirectory;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.RedirectStandardError = true;

            if (portNumber.HasValue && portNumber > 0)
            {
                if (isHttp)
                {
                    processStartInfo.EnvironmentVariables["Kestrel:Endpoints:Http:Url"] = "http://*:" + portNumber;
                }
                else
                {
                    processStartInfo.EnvironmentVariables["Kestrel:Endpoints:Https:Url"] = "https://*:" + portNumber;
                }
            }
            return Process.Start(processStartInfo);
        }

        /// <summary>
        /// Builds the path to the process's directory
        /// </summary>
        /// <param name="testAssemblyLocation">The path to the test's directory</param>
        /// <param name="appLocation">The path to the processes directory</param>
        /// <returns>The path to the directory for the given app</returns>
        private static string GetApplicationWorkingDirectory(string testAssemblyLocation, string appLocation)
        {
            string testedAppLocation = Path.GetDirectoryName(testAssemblyLocation)!;
            // e.g. microsoft-identity-web\tests\IntegrationTests\WebAppUiTests\bin\Debug\net6.0
            string[] segments = testedAppLocation.Split(Path.DirectorySeparatorChar);
            int numberSegments = segments.Length;
            int startLastSegments = numberSegments - 3;
            int endFirstSegments = startLastSegments - 2;
            return Path.Combine(
                Path.Combine(segments.Take(endFirstSegments).ToArray()),
                appLocation,
                Path.Combine(segments.Skip(startLastSegments).ToArray())
            );
        }

        /// <summary>
        /// Creates absolute path for Playwright trace file
        /// </summary>
        /// <param name="testAssemblyLocation">The path the test is being run from</param>
        /// <param name="traceName">The name for the zip file containing the trace</param>
        /// <returns>An absolute path to a Playwright Trace zip folder</returns>
        public static string GetTracePath(string testAssemblyLocation, string traceName)
        {
            const string traceParentFolder = "IntegrationTests";
            const string traceFolder = "PlaywrightTraces";
            const string zipExtension = ".zip";
            const int netVersionNumberLength = 3;

            int parentFolderIndex = testAssemblyLocation.IndexOf(traceParentFolder, StringComparison.InvariantCulture);
            string substring = testAssemblyLocation[..(parentFolderIndex + traceParentFolder.Length)];
            string netVersion = "_net" + Environment.Version.ToString()[..netVersionNumberLength];

            // e.g. [absolute path to repo root]\tests\IntegrationTests\PlaywrightTraces\[traceName]_net[versionNum].zip
            return Path.Combine(
                substring,
                traceFolder,
                traceName + netVersion + zipExtension
            );
        }

        /// <summary>
        /// Kills the processes in the queue and all of their children
        /// </summary>
        /// <param name="processQueue">queue of parent processes</param>
        [SupportedOSPlatform("windows")]
        public static void KillProcessTrees(Queue<Process> processQueue)
        {
            Process currentProcess;
            while (processQueue.Count > 0)
            {
                currentProcess = processQueue.Dequeue();
                if (currentProcess == null)
                    continue;

                foreach (Process child in GetChildProcesses(currentProcess))
                {
                    processQueue.Enqueue(child);
                }
                currentProcess.Kill();
                currentProcess.Close();
            }
        }

        /// <summary>
        /// Gets the child processes of a process on Windows
        /// </summary>
        /// <param name="process">The parent process</param>
        /// <returns>A list of child processes</returns>
        [SupportedOSPlatform("windows")]
        public static IList<Process> GetChildProcesses(this Process process)
        {
            ManagementObjectSearcher processSearch = new ManagementObjectSearcher($"Select * From Win32_Process Where ParentProcessID={process.Id}");
            IList<Process> processList = processSearch.Get()
                .Cast<ManagementObject>()
                .Select(mo =>
                    Process.GetProcessById(Convert.ToInt32(mo["ProcessID"], System.Globalization.CultureInfo.InvariantCulture)))
                .ToList();
            processSearch.Dispose();
            return processList;
        }

        /// <summary>
        /// Checks if all processes in a list are alive
        /// </summary>
        /// <param name="processes">List of processes to check</param>
        /// <returns>True if all are alive else false</returns>
        public static bool ProcessesAreAlive(List<Process> processes)
        {
            return processes.All(ProcessIsAlive);
        }

        /// <summary>
        /// Checks if a process is alive
        /// </summary>
        /// <param name="process">Process to check</param>
        /// <returns>True if alive false if not</returns>
        public static bool ProcessIsAlive(Process? process)
        {
            return !(process == null || process.HasExited);
        }

        /// <summary>
        /// Installs the chromium browser for Playwright enabling it to run even if no browser otherwise exists in the test environment
        /// </summary>
        /// <exception cref="Exception">Thrown if playwright is unable to install the browsers</exception>
        public static void InstallPlaywrightBrowser()
        {
            var exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
            if (exitCode != 0)
            {
                throw new Exception($"Playwright exited with code {exitCode}");
            }
        }
    }
    /// <summary>
    /// Fixture class that installs Playwright browser once per xunit test class that implements it
    /// </summary>
    public class InstallPlaywrightBrowserFixture : IDisposable
    {
        public InstallPlaywrightBrowserFixture()
        {
            UiTestHelpers.InstallPlaywrightBrowser();
        }
        public void Dispose()
        {
        }
    }
}

