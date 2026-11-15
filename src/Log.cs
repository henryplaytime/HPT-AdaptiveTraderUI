using System;
using BepInEx.Logging;

namespace AdaptiveTraderUI
{
    /// <summary>
    /// 统一日志出口。所有日志带固定前缀，方便用户/作者在 BepInEx\LogOutput.log 里搜索。
    /// 本模组的原则：任何异常都必须在这里被吞掉并记录，绝不向游戏循环抛出。
    /// </summary>
    internal static class Log
    {
        internal const string Prefix = "[HPT-AdaptiveTraderUI]";

        private static ManualLogSource _source;

        internal static void Init(ManualLogSource source)
        {
            _source = source;
        }

        internal static void Info(string message)
        {
            try { _source?.LogInfo(Prefix + " " + message); } catch { }
        }

        internal static void Warn(string message)
        {
            try { _source?.LogWarning(Prefix + " " + message); } catch { }
        }

        internal static void Error(string message)
        {
            try { _source?.LogError(Prefix + " " + message); } catch { }
        }

        internal static void Debug(string message)
        {
            try
            {
                var cfg = ModConfig.DebugLogging;
                if (cfg != null && cfg.Value)
                {
                    _source?.LogInfo(Prefix + " [debug] " + message);
                }
            }
            catch { }
        }

        internal static void Exception(string context, Exception ex)
        {
            Warn(context + " 发生异常（已捕获，不影响游戏）: " + ex.GetType().Name + ": " + ex.Message);
            Debug(context + " 异常堆栈: " + ex);
        }
    }
}
