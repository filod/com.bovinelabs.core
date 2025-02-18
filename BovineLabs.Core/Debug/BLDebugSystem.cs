﻿// <copyright file="BLDebugSystem.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

#if UNITY_EDITOR || BL_DEBUG
#define BL_DEBUG_UPDATE
#endif

namespace BovineLabs.Core
{
    using System;
    using System.IO;
    using BovineLabs.Core.ConfigVars;
    using BovineLabs.Core.Extensions;
    using Unity.Burst;
    using Unity.Entities;
    using Unity.Logging;
    using Unity.Logging.Internal;
    using Unity.Logging.Sinks;
    using Unity.Mathematics;
    using UnityEngine;

    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial class BLDebugSystem : SystemBase
    {
        internal const string LogLevelName = "debug.loglevel";
        internal const int LogLevelDefaultValue = (int)Unity.Logging.LogLevel.Error;

        [ConfigVar(LogLevelName, LogLevelDefaultValue, "The log level debugging for BovineLabs libraries.")]
        internal static readonly SharedStatic<int> LogLevel = SharedStatic<int>.GetOrCreate<BLDebugSystem>();

#if UNITY_EDITOR || BL_DEBUG
        private const LogLevel MinLogLevel = Unity.Logging.LogLevel.Debug;
#else
        private const LogLevel MinLogLevel = Unity.Logging.LogLevel.Warning;
#endif

        private LoggerHandle loggerHandle;
        private LogLevel currentLogLevel;

        private static event Action? Quitting;

        /// <inheritdoc />
        protected override void OnCreate()
        {
            var netDebugEntity = this.EntityManager.CreateEntity(ComponentType.ReadWrite<BLDebug>());
            this.EntityManager.SetName(netDebugEntity, "DBDebug");

            this.currentLogLevel = ToLogLevel(LogLevel.Data);
            var logDir = GetCurrentAbsoluteLogDirectory();

            var world = this.World.Name.TrimEnd("World").TrimEnd();

            var managerParameters = LogMemoryManagerParameters.Default;
#if UNITY_EDITOR
            // In editor we increase the default (64) capacity to allow verbose spamming
            managerParameters.InitialBufferCapacity = 1024 * 512;
#endif

            var logger = new LoggerConfig()
                .SyncMode.FatalIsSync()
                .WriteTo.JsonFile(
                    Path.Combine(logDir, "Output.log.json"),
                    minLevel: MinLogLevel,
                    outputTemplate: $"[{{Timestamp}}] {{Level}} | {world} | {{Message}}")
                .WriteTo.UnityDebugLog(
                    minLevel: this.currentLogLevel,
                    outputTemplate: $"{{Level}} | {world} | {{Message}}")
                .CreateLogger(managerParameters);

            this.loggerHandle = logger.Handle;
            var blDebug = new BLDebug { LoggerHandle = this.loggerHandle, Enabled = true };
            this.EntityManager.SetComponentData(netDebugEntity, blDebug);

#if !BL_DEBUG_UPDATE
            this.Enabled = false;
#endif

            Quitting += this.DisableLogger;
        }

        /// <inheritdoc />
        protected override void OnDestroy()
        {
            var logger = LoggerManager.GetLogger(this.loggerHandle);
            logger?.Dispose();

            Quitting -= this.DisableLogger;
        }

        /// <inheritdoc />
        protected override void OnUpdate()
        {
#if BL_DEBUG_UPDATE
            if ((int)this.currentLogLevel != LogLevel.Data)
            {
                this.currentLogLevel = ToLogLevel(LogLevel.Data);

                var logger = LoggerManager.GetLogger(this.loggerHandle);
                logger.GetSink<UnityDebugLogSink>().SetMinimalLogLevel(this.currentLogLevel);
            }
#endif
        }

        private static LogLevel ToLogLevel(int level)
        {
            return (LogLevel)math.clamp(level, 0, (int)Unity.Logging.LogLevel.Fatal);
        }

        /// <summary> <see cref="Unity.Logging.DefaultSettings.GetLogDirectory" />. </summary>
        private static string GetCurrentAbsoluteLogDirectory()
        {
#if UNITY_EDITOR
            var dataDir = Path.GetDirectoryName(Application.dataPath)!;
            var logDir = Path.Combine(dataDir, "Logs");
            Directory.CreateDirectory(logDir);
            return logDir;

#else
            var logPath = string.IsNullOrEmpty(Application.consoleLogPath)
                ? Application.persistentDataPath
                : Application.consoleLogPath;

            var logDir = Path.Combine(Path.GetDirectoryName(logPath)!, "Logs");
            Directory.CreateDirectory(logDir);
            return logDir;
#endif
        }

        // In 1.0.11 when leaving play mode the log handle gets disposed before the world causing errors in OnDestroy/OnStopRunning that try to use it
        // This is a gross workaround to disable the logger before anything can call it during a shutdown
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            Application.quitting += OnQuit;
        }

        private static void OnQuit()
        {
            Application.quitting -= OnQuit;
            Quitting?.Invoke();
        }

        private void DisableLogger()
        {
            ref var debug = ref this.EntityManager.GetSingletonRW<BLDebug>().ValueRW;
            debug.Enabled = false;
        }
    }
}
