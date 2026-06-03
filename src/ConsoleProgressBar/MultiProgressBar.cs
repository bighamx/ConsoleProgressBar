using System;
using System.Collections.Generic;

namespace Chuckie.ConsoleProgressBar
{
    /// <summary>
    /// 多进度条管理器 - 支持同时显示多个进度条（每个可使用不同样式）
    /// </summary>
    public class MultiProgressBar : IDisposable
    {
        private readonly List<ProgressBarInfo> _progressBars = new List<ProgressBarInfo>();
        private int _startRow;
        private readonly int _barWidth;
        private readonly object _lock = new object();

        private class ProgressBarInfo
        {
            public string Name { get; set; }
            public long Total { get; set; }
            public long Current { get; set; }
            public string Message { get; set; }
            public ConsoleColor Color { get; set; }
            public DateTime StartTime { get; set; }
            public ProgressBarChars Chars { get; set; }
        }

        public MultiProgressBar(int barWidth = 30)
        {
            _startRow = Console.CursorTop;
            _barWidth = barWidth;
        }

        /// <summary>
        /// 添加一个进度条
        /// </summary>
        /// <param name="name">进度条名称</param>
        /// <param name="total">总数</param>
        /// <param name="color">颜色</param>
        /// <param name="style">样式</param>
        public int AddProgressBar(string name, long total, ConsoleColor color = ConsoleColor.Green, ProgressBarStyle style = ProgressBarStyle.Classic)
        {
            return AddProgressBar(name, total, color, ProgressBarChars.FromStyle(style));
        }

        /// <summary>
        /// 添加一个进度条（使用自定义字符配置）
        /// </summary>
        public int AddProgressBar(string name, long total, ConsoleColor color, ProgressBarChars chars)
        {
            lock (_lock)
            {
                var info = new ProgressBarInfo
                {
                    Name = name,
                    Total = total,
                    Current = 0,
                    Message = "",
                    Color = color,
                    StartTime = DateTime.Now,
                    Chars = chars ?? ProgressBarChars.Classic
                };
                _progressBars.Add(info);

                // 预留空间
                int oldTop = Console.CursorTop;
                Console.WriteLine();
                if (Console.CursorTop == oldTop) _startRow--;

                Render();
                return _progressBars.Count - 1;
            }
        }

        /// <summary>
        /// 更新指定进度条
        /// </summary>
        public void Update(int index, long current, string message = null)
        {
            lock (_lock)
            {
                if (index < 0 || index >= _progressBars.Count) return;

                var bar = _progressBars[index];
                bar.Current = Math.Min(current, bar.Total);
                if (message != null)
                    bar.Message = message;

                RenderBar(index);
            }
        }

        /// <summary>
        /// 增加指定进度条的进度
        /// </summary>
        public void Increment(int index, long increment = 1, string message = null)
        {
            lock (_lock)
            {
                if (index < 0 || index >= _progressBars.Count) return;
                Update(index, _progressBars[index].Current + increment, message);
            }
        }

        private void Render()
        {
            for (int i = 0; i < _progressBars.Count; i++)
            {
                RenderBar(i);
            }
        }

        private void RenderBar(int index)
        {
            try
            {
                var bar = _progressBars[index];
                int row = _startRow + index;

                // 隐藏光标减少闪烁
                TrySetCursorVisibility(false);

                Console.SetCursorPosition(0, row);

                double percentage = bar.Total > 0 ? (double)bar.Current / bar.Total : 0;

                // 输出名称
                string prefix = $"{bar.Name}: [";
                Console.Write(prefix);

                var originalColor = Console.ForegroundColor;

                // 使用共享的静态方法渲染进度条内容
                ConsoleProgressBar.RenderProgressBarContentStatic(bar.Chars, percentage, _barWidth, bar.Color, ConsoleColor.DarkGray);

                Console.ForegroundColor = originalColor;

                // 使用与 ConsoleProgressBar 一致的格式: ] 百分比 | 详情
                string percentText = $"] {percentage * 100:F1}% | ";
                Console.Write(percentText);

                // 使用共享方法生成详情（不包含 message，因为名称已在前面显示）
                string details = ConsoleProgressBar.BuildProgressDetails(bar.Current, bar.Total, bar.StartTime, percentage, bar.Message, includeMessage: true);
                
                // 截断过长的详情，防止换行覆盖下一个进度条
                int currentLeft = prefix.Length + _barWidth + percentText.Length;
                int maxDetailWidth = Console.WindowWidth - currentLeft - 1;
                
                if (maxDetailWidth > 0)
                {
                    if (details.Length > maxDetailWidth)
                    {
                        details = details.Substring(0, maxDetailWidth - 3) + "...";
                    }
                    Console.Write(details);
                }

                // 清除行尾
                int remaining = Console.WindowWidth - Console.CursorLeft - 1;
                if (remaining > 0)
                    Console.Write(new string(' ', remaining));

                // 移动光标到进度条区域之后
                Console.SetCursorPosition(0, _startRow + _progressBars.Count);

                // 恢复光标
                TrySetCursorVisibility(true);
            }
            catch { }
        }

        /// <summary>
        /// 安全地设置光标可见性（跨平台兼容）
        /// </summary>
        private static void TrySetCursorVisibility(bool visible)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    Console.CursorVisible = visible;
                }
            }
            catch
            {
                // 忽略异常
            }
        }

        public void Dispose()
        {
            try
            {
                // 确保光标移动到所有进度条下方
                Console.SetCursorPosition(0, _startRow + _progressBars.Count);
                Console.WriteLine(); // 额外换行确保后续输出不会覆盖
            }
            catch { }
        }
    }
}
