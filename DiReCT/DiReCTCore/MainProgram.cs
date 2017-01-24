﻿/*
 * Copyright (c) 2016 Academia Sinica, Institude of Information Science
 *
 * License:
 *      GPL 3.0 : This file is subject to the terms and conditions defined
 *      in file 'COPYING.txt', which is part of this source code package.
 *
 * Project Name:
 * 
 *      DiReCT(Disaster Record Capture Tool)
 * 
 * File Description:
 * File Name:
 * 
 *      MainProgram.cs
 * 
 * Abstract:
 *      
 *      The main program of DiReCT creates all resources including 
 *      sychronization objects, work queues, worker threads, UI threads,
 *      and turn itself into a UI helper thread.
 *
 * Authors:
 * 
 *      Hunter Hsieh, hunter205@iis.sinica.edu.tw  
 *      Jeff Chen, jeff@iis.sinica.edu.tw
 * 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Diagnostics;
using Microsoft.Win32;
using System.IO;

namespace DiReCT
{
    static class Constants
    {
        // Time interval constants
        public const int VERY_VERY_SHORT_TIME = 100; // 0.1 seconds
        public const int VERY_SHORT_TIME = 1000; // 1000 milliseconds, 1 second 
        public const int SHORT_TIME = 10000; // 10000 milliseconds, 10 seconds
        public const int LONG_TIME = 60000; // 60000 milliseconds, 1 minute
        public const int VERY_LONG_TIME = 300000; // 300000 milliseconds,
                                                  // 5 minutes
    }

    #region Program Definitions
    public enum ModuleThread
    {
        AAA = 0,    // Authentication and Authorization module
        DM,         // Data Manager module
        DS,         // Data Synchronizer module
        MAN,        // Monitor and Notification module
        RTQC,       // Real-time Quality Control module
        NumberOfModules
    };

    public enum TimeInterval
    {
        VeryVeryShortTime = Constants.VERY_VERY_SHORT_TIME,
        VeryShortTime = Constants.VERY_SHORT_TIME,   
        ShortTime = Constants.SHORT_TIME,      
        LongTime = Constants.LONG_TIME,       
        VeryLongTime = Constants.VERY_LONG_TIME   
    };
    #endregion

    #region Program Shared Data
    class ThreadParameters
    {
        // Module initialization failed Event
        // Pass by reference to ModuleInitFailedEvents[]
        public AutoResetEvent ModuleInitFailedEvent;

        // Module thread initialization complete and ready to work
        // Pass by reference to ModuleReadyEvents[]
        public AutoResetEvent ModuleReadyEvent;

        public ThreadParameters()
        {
            ModuleInitFailedEvent = new AutoResetEvent(false);
            ModuleReadyEvent = new AutoResetEvent(false);
        }
    }
    class ModuleControlDataBlock
    {
        public ThreadParameters ThreadParameters;
        public ModuleControlDataBlock()
        {
            ThreadParameters = new ThreadParameters();
        }
    }
    #endregion


    // The main entry point of DiReCT application
    class DiReCTMainProgram : Application
    {
        private static Thread[] ModuleThreadHandles;
        private static Thread UIThreadHandle;
        private static ModuleControlDataBlock[] ModuleControlDataBlocks;
        private static AutoResetEvent[] ModuleReadyEvents;
        private static AutoResetEvent[] ModuleInitFailedEvents;
        public static ManualResetEvent ModuleStartWorkEvent 
            = new ManualResetEvent(false);

        private static bool InitHasFailed = false; // Whether initialization 
                                                   // processes were completed
                                                   // in time
        private static Timer InitializationTimer;

        [MTAThread]
        static void Main()
        {
            // Subscribe system log off/shutdown or application close events
            SystemEvents.SessionEnding
                += new SessionEndingEventHandler(ShutdownEventHandler);
            AppDomain.CurrentDomain.ProcessExit
                += new EventHandler(ShutdownEventHandler);

            // Initialize objects for threads
            // Initialize thread objects and control data block of each modules
            try
            {
                ModuleThreadHandles
                    = new Thread[(int)ModuleThread.NumberOfModules];
                ModuleControlDataBlocks = new ModuleControlDataBlock[
                    (int)ModuleThread.NumberOfModules];
                ModuleReadyEvents = new AutoResetEvent[
                    (int)ModuleThread.NumberOfModules];
                ModuleInitFailedEvents = new AutoResetEvent[
                    (int)ModuleThread.NumberOfModules];
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                Debug.WriteLine("Thread resource creation failed.");
                goto CleanupExit;
            }
            
            // AAA Module
            try
            {
                // Create and intialize AAA module
                ModuleThreadHandles[(int)ModuleThread.AAA]
                    = new Thread(AAAModule.AAAInit);
                ModuleControlDataBlocks[(int)ModuleThread.AAA]
                    = new ModuleControlDataBlock();
                ModuleThreadHandles[(int)ModuleThread.AAA]
                        .Start(ModuleControlDataBlocks[(int)ModuleThread.AAA]
                        .ThreadParameters);
            }
            catch (ArgumentNullException ex)
            {
                Debug.WriteLine(ex.Message);
                Debug.WriteLine("AAA module thread creation failed.");
                goto CleanupExit;
            }

            // DM Module
            try
            {
                // Create and intialize DM module
                ModuleThreadHandles[(int)ModuleThread.DM]
                        = new Thread(DMModule.DMInit);
                ModuleControlDataBlocks[(int)ModuleThread.DM]
                    = new ModuleControlDataBlock();
                ModuleThreadHandles[(int)ModuleThread.DM]
                        .Start(ModuleControlDataBlocks[(int)ModuleThread.DM]
                        .ThreadParameters);
            }
            catch (ArgumentNullException ex)
            {
                Debug.WriteLine(ex.Message);
                Debug.WriteLine("DM module thread creation failed.");
                goto CleanupExit;
            }

            // DS Module
            try
            {
                // Create and intialize DS module
                ModuleThreadHandles[(int)ModuleThread.DS]
                        = new Thread(DSModule.DSInit);
                ModuleControlDataBlocks[(int)ModuleThread.DS]
                    = new ModuleControlDataBlock();
                ModuleThreadHandles[(int)ModuleThread.DS]
                        .Start(ModuleControlDataBlocks[(int)ModuleThread.DS]
                        .ThreadParameters);
            }
            catch (ArgumentNullException ex)
            {
                Debug.WriteLine(ex.Message);
                Debug.WriteLine("DS module thread creation failed.");
                goto CleanupExit;
            }

            // MAN Module
            try
            {
                // Create and intialize MAN module
                ModuleThreadHandles[(int)ModuleThread.MAN]
                        = new Thread(MANModule.MANInit);
                ModuleControlDataBlocks[(int)ModuleThread.MAN]
                    = new ModuleControlDataBlock();
                ModuleThreadHandles[(int)ModuleThread.MAN]
                        .Start(ModuleControlDataBlocks[(int)ModuleThread.MAN]
                        .ThreadParameters);
            }
            catch (ArgumentNullException ex)
            {
                Debug.WriteLine(ex.Message);
                Debug.WriteLine("MAN module thread creation failed.");
                goto CleanupExit;
            }

            // RTQC Module
            try
            {
                // Create and intialize RTQC module
                ModuleThreadHandles[(int)ModuleThread.RTQC]
                        = new Thread(RTQCModule.RTQCInit);
                ModuleControlDataBlocks[(int)ModuleThread.RTQC]
                    = new ModuleControlDataBlock();
                ModuleThreadHandles[(int)ModuleThread.RTQC]
                        .Start(ModuleControlDataBlocks[(int)ModuleThread.RTQC]
                        .ThreadParameters);
            }
            catch (ArgumentNullException ex)
            {
                Debug.WriteLine(ex.Message);
                Debug.WriteLine("RTQC module thread creation failed.");
                goto CleanupExit;
            }

            // Initialize the thread object of GUI thread
            try
            {
                UIThreadHandle = new Thread(UIMainFunction);
                UIThreadHandle.SetApartmentState(ApartmentState.STA);
            }
            catch (ArgumentNullException ex)
            {
                Debug.WriteLine(ex.Message);
                Debug.WriteLine("UI thread creation failed.");
                goto CleanupExit;
            }

            // Initialize the array of ready events for WaitHandle
            for (int i = 0; i < (int)ModuleThread.NumberOfModules; i++)
            {
                ModuleReadyEvents[i]
                    = ModuleControlDataBlocks[i].ThreadParameters
                                                .ModuleReadyEvent;
                ModuleInitFailedEvents[i]
                    = ModuleControlDataBlocks[i].ThreadParameters
                                                .ModuleInitFailedEvent;
            }

            while (!InitHasFailed)
            {
                if (WaitHandle.WaitAll(ModuleReadyEvents,
                                       (int)TimeInterval.VeryLongTime,
                                       true))
                {
                    Debug.WriteLine(
                        "Phase 1 initialization of all modules complete!");
                    break;
                }
                else
                {
                    int WaitReturnValue
                       = WaitHandle.WaitAny(ModuleInitFailedEvents,
                                            (int)TimeInterval.VeryVeryShortTime,
                                            true);
                    if (WaitReturnValue != WaitHandle.WaitTimeout)
                    {
                        InitHasFailed = true;
                        goto CleanupExit;
                    }
                }
            }

            ModuleStartWorkEvent.Set();            

            //Start to execute UI
            try
            {
                UIThreadHandle.Start();
            }
            catch (ArgumentNullException ex)
            {
                Debug.WriteLine(ex.Message);
                Debug.WriteLine("UI thread can not start!");
                goto CleanupExit;
            }

            //
            // Code of Helper thread here...
            //

            UIThreadHandle.Join();

CleanupExit:

            Debug.WriteLine("Enter CleanupExit.");

            // Signal all created threads to prepare to terminate
            foreach (Thread moduleThreadHandle in ModuleThreadHandles)
            {
                if (moduleThreadHandle.ThreadState
                    != System.Threading.ThreadState.Unstarted)
                {
                    moduleThreadHandle.Abort();
                }
            }

            InitializationTimer 
                = new Timer(new TimerCallback(AbortTimeOutEventHandler),
                            ModuleThreadHandles,
                            (int)TimeInterval.LongTime,
                            Timeout.Infinite); // Callback is invoked once

            // Wait for all created threads to terminate
            foreach (Thread moduleThreadHandle in ModuleThreadHandles)
            {
                if (moduleThreadHandle.ThreadState
                    != System.Threading.ThreadState.Unstarted)
                {
                    moduleThreadHandle.Join();
                }
            }
            return;
        }
        
        private static void UIMainFunction()
        {
            DiReCTMainProgram DiReCTmainProgram = new DiReCTMainProgram();
            DiReCTmainProgram.StartupUri 
                = new System.Uri("DiReCTCore\\MainWindow.xaml", 
                                 System.UriKind.Relative);
            DiReCTmainProgram.Run();
        }

        private static void AbortTimeOutEventHandler(object state)
        {
            for (int i = 0; i < ModuleThreadHandles.Length; i++)
            {
                Thread moduleThreadHandle = ModuleThreadHandles[i];
                if (moduleThreadHandle.IsAlive)
                {
                    string ModuleName
                        = Enum.GetName(typeof(ModuleThread), i);
                    Debug.WriteLine(
                        ModuleName + "module thread aborting timed out.");
                }
            }
            return;
        }

        private static void ShutdownEventHandler(object sender, EventArgs e)
        {
            Debug.WriteLine("Windows is shutting down...");
            Debug.WriteLine("Cleaning up...");


            //
            // Cannot goto CleanupExit
            // Do clean up here...
            //
Cleanup:
            // Signal all created threads to prepare to terminate
            foreach (Thread moduleThreadHandle in ModuleThreadHandles)
            {
                if (moduleThreadHandle.ThreadState
                    != System.Threading.ThreadState.Unstarted)
                {
                    moduleThreadHandle.Abort();
                }
            }

            InitializationTimer
                = new Timer(new TimerCallback(AbortTimeOutEventHandler),
                            ModuleThreadHandles,
                            (int)TimeInterval.LongTime,
                            Timeout.Infinite); // Callback is invoked once

            // Wait for all created threads to terminate
            foreach (Thread moduleThreadHandle in ModuleThreadHandles)
            {
                if (moduleThreadHandle.ThreadState
                    != System.Threading.ThreadState.Unstarted)
                {
                    moduleThreadHandle.Join();
                }
            }
            return;

            //File.AppendAllText("Log.txt", DateTime.Now.ToString()+" DiReCT is shutting down..."+Environment.NewLine, Encoding.Unicode);
        }
    }    
}