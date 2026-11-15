using System;
using System.IO;
using System.Text;

namespace AdaptiveTraderUI
{
    /// <summary>
    /// 诊断输出：仅在 [Advanced] Diagnostic Dump = true 时，把界面A 的诊断快照写入
    /// BepInEx\HPT-AdaptiveTraderUI-diag.txt。默认关闭，正常游玩不会产生该文件。
    /// 独立文件比翻 BepInEx\LogOutput.log 更方便发给作者；文件只是追加文本，删除无副作用。
    /// </summary>
    internal static class DiagFile
    {
        private static readonly object Gate = new object();
        private static bool _headerWritten;
        private static string _path;
        private static bool _writeFailedWarned;

        internal static string Path
        {
            get
            {
                if (_path != null)
                {
                    return _path;
                }

                try
                {
                    _path = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "HPT-AdaptiveTraderUI-diag.txt");
                }
                catch
                {
                    _path = "HPT-AdaptiveTraderUI-diag.txt";
                }

                return _path;
            }
        }

        internal static void Append(string tag, string body)
        {
            if (!ModConfig.DiagnosticDumpEnabled)
            {
                return;
            }

            try
            {
                lock (Gate)
                {
                    var sb = new StringBuilder();
                    if (!_headerWritten)
                    {
                        _headerWritten = true;
                        sb.AppendLine("############################################################");
                        sb.AppendLine("# HPT-AdaptiveTraderUI v" + Plugin.PluginVersion + " DIAG 诊断输出");
                        sb.AppendLine("# 生成时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        sb.AppendLine("# 用途: 定位界面A（顶部商人卡条）滚动/空白/缺卡问题。");
                        sb.AppendLine("# 仅在 [Advanced] Diagnostic Dump = true 时生成；删除本文件无任何副作用。");
                        sb.AppendLine("############################################################");
                        sb.AppendLine();
                    }

                    sb.AppendLine("===== [" + tag + "] " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " =====");
                    sb.AppendLine(body);
                    sb.AppendLine();
                    File.AppendAllText(Path, sb.ToString(), Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                if (!_writeFailedWarned)
                {
                    _writeFailedWarned = true;
                    Log.Warn("诊断文件写入失败（不影响游戏）: " + ex.Message);
                }
            }
        }

        internal static void Hint()
        {
            try
            {
                Log.Info("诊断输出已启用：界面A 的详细诊断会写入 " + Path
                         + "。复现问题后把该文件发给作者即可。");
            }
            catch
            {
            }
        }
    }
}
