using System;
using System.Text;

namespace Chuckie.ConsoleProgressBar
{
    /// <summary>
    /// 进度条字符样式枚举
    /// </summary>
    public enum ProgressBarStyle
    {
        /// <summary>
        /// 自动检测：如果控制台支持Unicode则使用Classic，否则使用Ascii
        /// </summary>
        Auto,

        /// <summary>
        /// 经典块状样式: █░
        /// </summary>
        Classic,

        /// <summary>
        /// 盲文样式 (Docker风格): ⣿⣀
        /// </summary>
        Braille,

        /// <summary>
        /// 精细盲文样式 (8级渐变)
        /// </summary>
        BrailleFine,

        /// <summary>
        /// ASCII样式: #- （最佳兼容性）
        /// </summary>
        Ascii,

        /// <summary>
        /// 箭头样式: =>-
        /// </summary>
        Arrow,

        /// <summary>
        /// 圆点样式: •◦
        /// </summary>
        Dots,

        /// <summary>
        /// 大圆点样式: ●○（全角，双宽度）
        /// </summary>
        DotsBig,

        /// <summary>
        /// 方块渐变样式: ▓▒░
        /// </summary>
        Gradient,

        /// <summary>
        /// 细线样式: ━╸
        /// </summary>
        Line,

        /// <summary>
        /// 自定义样式
        /// </summary>
        Custom
    }

    /// <summary>
    /// 进度条字符配置
    /// </summary>
    public class ProgressBarChars
    {
        /// <summary>
        /// 已完成部分的填充字符
        /// </summary>
        public char FilledChar { get; set; }

        /// <summary>
        /// 未完成部分的填充字符
        /// </summary>
        public char EmptyChar { get; set; }

        /// <summary>
        /// 进度头部字符（可选，用于箭头等样式）
        /// </summary>
        public char? HeadChar { get; set; }

        /// <summary>
        /// 精细填充字符数组（用于支持多级渐变的样式）
        /// 从空到满的字符序列
        /// </summary>
        public char[] GradientChars { get; set; }

        /// <summary>
        /// 是否使用精细渐变模式
        /// </summary>
        public bool UseGradient => GradientChars is { Length: > 0 };

        /// <summary>
        /// 是否使用双宽度字符（如全角字符）
        /// 使用双宽度字符时，实际字符数量会减半以保持视觉宽度一致
        /// </summary>
        public bool IsDoubleWidth { get; set; }

        // 预定义样式
        public static ProgressBarChars Classic => new ProgressBarChars { FilledChar = '█', EmptyChar = '░' };
        public static ProgressBarChars Braille => new ProgressBarChars { FilledChar = '⣿', EmptyChar = '⣀' };
        public static ProgressBarChars BrailleFine => new ProgressBarChars
        {
            FilledChar = '⣿',
            EmptyChar = '⣀',
            GradientChars = ['⣀', '⣄', '⣤', '⣦', '⣶', '⣷', '⣿']
        };
        public static ProgressBarChars Ascii => new ProgressBarChars { FilledChar = '#', EmptyChar = '-' };
        public static ProgressBarChars Arrow => new ProgressBarChars { FilledChar = '=', EmptyChar = '-', HeadChar = '>' };
        public static ProgressBarChars Dots => new ProgressBarChars { FilledChar = '•', EmptyChar = '◦' };
        public static ProgressBarChars DotsBig => new ProgressBarChars { FilledChar = '●', EmptyChar = '○', IsDoubleWidth = true };
        public static ProgressBarChars Gradient => new ProgressBarChars
        {
            FilledChar = '█',
            EmptyChar = '░',
            GradientChars = ['░', '▒', '▓', '█']
        };
        public static ProgressBarChars Line => new ProgressBarChars { FilledChar = '━', EmptyChar = '╸' };

        /// <summary>
        /// 检测控制台是否支持Unicode输出
        /// </summary>
        public static bool IsUnicodeSupported()
        {
            try
            {
                // 检查输出编码是否支持Unicode
                var encoding = Console.OutputEncoding;
                return encoding.CodePage == 65001 || // UTF-8
                       encoding is UnicodeEncoding ||
                       encoding is UTF32Encoding ||
                       encoding.EncodingName.IndexOf("Unicode", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 启用控制台Unicode支持（设置UTF-8编码）
        /// </summary>
        public static void EnableUnicodeSupport()
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
            }
            catch
            {
                // 忽略异常
            }
        }

        /// <summary>
        /// 根据样式枚举获取字符配置
        /// </summary>
        public static ProgressBarChars FromStyle(ProgressBarStyle style)
        {
            // Auto模式自动检测
            if (style == ProgressBarStyle.Auto)
            {
                return IsUnicodeSupported() ? Classic : Ascii;
            }

            return style switch
            {
                ProgressBarStyle.Classic => Classic,
                ProgressBarStyle.Braille => Braille,
                ProgressBarStyle.BrailleFine => BrailleFine,
                ProgressBarStyle.Ascii => Ascii,
                ProgressBarStyle.Arrow => Arrow,
                ProgressBarStyle.Dots => Dots,
                ProgressBarStyle.DotsBig => DotsBig,
                ProgressBarStyle.Gradient => Gradient,
                ProgressBarStyle.Line => Line,
                _ => Classic
            };
        }
    }

    /// <summary>
    /// 控制台进度条组件
    /// 支持底部悬浮显示，日志在上方滚动，不受控制台滚动影响
    /// </summary>
    public class ConsoleProgressBar : IDisposable
    {
        private int _fixedRow;           // 固定显示的行号
        private readonly int _totalWidth;         // 进度条总宽度
        private readonly ProgressBarChars _chars; // 进度条字符配置
        private readonly ConsoleColor _progressColor;   // 进度条颜色
        private readonly ConsoleColor _backgroundColor; // 背景颜色
        private readonly object _lock = new object();

        private long _total;          // 总数
        private long _current;        // 当前进度
        private string _message;      // 附加消息
        private DateTime _startTime;  // 开始时间
        private bool _isCompleted;    // 是否已完成
        private int _originalCursorTop; // 原始光标位置

        /// <summary>
        /// 当前进度值
        /// </summary>
        public long Current => _current;

        /// <summary>
        /// 总数
        /// </summary>
        public long Total => _total;

        /// <summary>
        /// 进度百分比 (0-100)
        /// </summary>
        public double Percentage => _total > 0 ? (double)_current / _total * 100 : 0;

        /// <summary>
        /// 是否已完成
        /// </summary>
        public bool IsCompleted => _isCompleted;

        /// <summary>
        /// 创建进度条实例
        /// </summary>
        /// <param name="total">总数量</param>
        /// <param name="message">初始消息</param>
        /// <param name="totalWidth">进度条宽度（默认50）</param>
        /// <param name="style">进度条样式（默认BrailleFine精细盲文样式）</param>
        /// <param name="progressColor">进度条颜色</param>
        /// <param name="backgroundColor">背景颜色</param>
        public ConsoleProgressBar(
            long total,
            string message = "",
            int totalWidth = 50,
            ProgressBarStyle style = ProgressBarStyle.BrailleFine,
            ConsoleColor progressColor = ConsoleColor.Green,
            ConsoleColor backgroundColor = ConsoleColor.DarkGray)
            : this(total, message, totalWidth, ProgressBarChars.FromStyle(style), progressColor, backgroundColor)
        {
        }

        /// <summary>
        /// 创建进度条实例（使用自定义字符配置）
        /// </summary>
        /// <param name="total">总数量</param>
        /// <param name="message">初始消息</param>
        /// <param name="totalWidth">进度条宽度（默认50）</param>
        /// <param name="chars">自定义字符配置</param>
        /// <param name="progressColor">进度条颜色</param>
        /// <param name="backgroundColor">背景颜色</param>
        public ConsoleProgressBar(
            long total,
            string message,
            int totalWidth,
            ProgressBarChars chars,
            ConsoleColor progressColor = ConsoleColor.Green,
            ConsoleColor backgroundColor = ConsoleColor.DarkGray)
        {
            _total = total;
            _current = 0;
            _message = message;
            _totalWidth = totalWidth;
            _chars = chars ?? ProgressBarChars.Classic;
            _progressColor = progressColor;
            _backgroundColor = backgroundColor;
            _startTime = DateTime.Now;
            _isCompleted = false;

            _originalCursorTop = Console.CursorTop;
            _fixedRow = _originalCursorTop;

            // 预留进度条显示空间（2行：进度条 + 详情）
            int oldTop = Console.CursorTop;
            Console.WriteLine();
            if (Console.CursorTop == oldTop) _fixedRow--;
            
            oldTop = Console.CursorTop;
            Console.WriteLine();
            if (Console.CursorTop == oldTop) _fixedRow--;

            // 初始绘制
            Render();
        }

        /// <summary>
        /// 创建进度条实例（兼容旧API，使用自定义字符）
        /// </summary>
        [Obsolete("请使用 ProgressBarStyle 或 ProgressBarChars 参数的构造函数")]
        public ConsoleProgressBar(
            long total,
            string message,
            int totalWidth,
            char progressChar,
            char emptyChar,
            ConsoleColor progressColor = ConsoleColor.Green,
            ConsoleColor backgroundColor = ConsoleColor.DarkGray)
            : this(total, message, totalWidth,
                  new ProgressBarChars { FilledChar = progressChar, EmptyChar = emptyChar },
                  progressColor, backgroundColor)
        {
        }

        private DateTime _lastRenderTime = DateTime.MinValue; // 上次渲染时间
        private double _lastRenderPercentage = -1; // 上次渲染进度
        private readonly TimeSpan _renderInterval = TimeSpan.FromMilliseconds(200); // 渲染间隔（200ms = 5次/秒）
        private const double _renderThreshold = 0.001; // 渲染阈值（进度变化超过0.01%才渲染）

        /// <summary>
        /// 更新进度
        /// </summary>
        /// <param name="current">当前进度值</param>
        /// <param name="message">可选的消息更新</param>
        public void Update(long current, string message = null)
        {
            lock (_lock)
            {
                _current = Math.Min(current, _total);
                if (message != null)
                    _message = message;

                double percentage = _total > 0 ? (double)_current / _total : 0;

                // 只有当满足以下条件之一时才渲染：
                // 1. 进度已完成
                // 2. (距离上次渲染超过了间隔时间 AND 进度变化超过阈值)
                // 3. 刚开始（当前值为0或1）或上次未渲染过
                bool timeThresholdMet = DateTime.Now - _lastRenderTime >= _renderInterval;
                bool diffThresholdMet = Math.Abs(percentage - _lastRenderPercentage) >= _renderThreshold;

                if (_current >= _total || (timeThresholdMet && diffThresholdMet) || _current <= 1 || _lastRenderPercentage < 0)
                {
                    Render();
                    _lastRenderTime = DateTime.Now;
                    _lastRenderPercentage = percentage;
                    
                    if (_current >= _total && !_isCompleted)
                    {
                        _isCompleted = true;
                    }
                }
            }
        }

        /// <summary>
        /// 增加进度
        /// </summary>
        /// <param name="increment">增量（默认1）</param>
        /// <param name="message">可选的消息更新</param>
        public void Increment(long increment = 1, string message = null)
        {
            Update(_current + increment, message);
        }

        /// <summary>
        /// 设置总数（用于动态调整）
        /// </summary>
        /// <param name="total">新的总数</param>
        public void SetTotal(long total)
        {
            lock (_lock)
            {
                _total = total;
                _isCompleted = false;
                Render();
            }
        }

        /// <summary>
        /// 重置进度条
        /// </summary>
        /// <param name="total">可选的新总数</param>
        /// <param name="message">可选的新消息</param>
        public void Reset(long? total = null, string message = null)
        {
            lock (_lock)
            {
                _current = 0;
                if (total.HasValue)
                    _total = total.Value;
                if (message != null)
                    _message = message;
                _startTime = DateTime.Now;
                _isCompleted = false;
                Render();
            }
        }

        /// <summary>
        /// 渲染进度条到固定行
        /// </summary>
        private void Render()
        {
            lock (_lock)
            {
                try
                {
                    // 尝试隐藏光标以减少闪烁（仅Windows平台支持）
                    TrySetCursorVisibility(false);

                    // 移动光标到固定行
                    Console.SetCursorPosition(0, _fixedRow);

                    // 计算进度
                    double percentage = _total > 0 ? (double)_current / _total : 0;

                    // 保存当前颜色
                    var originalForeground = Console.ForegroundColor;

                    // 输出进度条开头
                    Console.Write('[');

                    // 渲染进度条内容
                    RenderProgressBarContent(percentage);

                    // 恢复颜色
                    Console.ForegroundColor = originalForeground;

                    // 输出百分比
                    string percentText = $"] {percentage * 100:F1}%";
                    Console.Write(percentText);

                    // 清除行尾多余字符
                    int remainingSpace = Console.WindowWidth - Console.CursorLeft - 1;
                    if (remainingSpace > 0)
                        Console.Write(new string(' ', remainingSpace));

                    // 第二行：详细信息
                    Console.SetCursorPosition(0, _fixedRow + 1);

                    // 使用共享方法生成详情
                    string detailLine = " " + BuildProgressDetails(_current, _total, _startTime, percentage, _message);

                    // 截断过长的详情
                    int maxDetailWidth = Console.WindowWidth - 1;
                    if (detailLine.Length > maxDetailWidth)
                    {
                        detailLine = detailLine.Substring(0, maxDetailWidth - 3) + "...";
                    }

                    Console.Write(detailLine);

                    // 清除行尾多余字符
                    remainingSpace = Console.WindowWidth - Console.CursorLeft - 1;
                    if (remainingSpace > 0)
                        Console.Write(new string(' ', remainingSpace));

                    // 将光标移动到进度条区域下方（而不是恢复到之前位置）
                    Console.SetCursorPosition(0, _fixedRow + 2);

                    // 恢复光标可见性
                    TrySetCursorVisibility(true);
                }
                catch (Exception)
                {
                    // 忽略控制台操作异常（如重定向输出时）
                }
            }
        }

        /// <summary>
        /// 安全地设置光标可见性（跨平台兼容）
        /// </summary>
        private static void TrySetCursorVisibility(bool visible)
        {
            try
            {
#if NET5_0_OR_GREATER
                if (OperatingSystem.IsWindows())
                {
                    Console.CursorVisible = visible;
                }
#else
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    Console.CursorVisible = visible;
                }
#endif
            }
            catch
            {
                // 忽略异常
            }
        }

        /// <summary>
        /// 格式化时间跨度（静态方法供复用）
        /// </summary>
        internal static string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalDays >= 1)
                return $"{(int)ts.TotalDays}:{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            return $"{ts.Minutes:D2}:{ts.Seconds:D2}";
        }

        /// <summary>
        /// 渲染进度条内容（静态方法供复用）
        /// </summary>
        /// <param name="chars">字符配置</param>
        /// <param name="percentage">进度百分比 (0-1)</param>
        /// <param name="barWidth">进度条宽度</param>
        /// <param name="progressColor">进度颜色</param>
        /// <param name="backgroundColor">背景颜色</param>
        internal static void RenderProgressBarContentStatic(ProgressBarChars chars, double percentage, int barWidth, ConsoleColor progressColor, ConsoleColor backgroundColor)
        {
            if (chars.UseGradient)
            {
                // 精细渐变模式
                int gradientLevels = chars.GradientChars.Length;
                double totalUnits = barWidth * gradientLevels;
                double filledUnits = percentage * totalUnits;

                int fullFilledChars = (int)(filledUnits / gradientLevels);
                int partialLevel = (int)(filledUnits % gradientLevels);
                int emptyChars = barWidth - fullFilledChars - (partialLevel > 0 ? 1 : 0);

                Console.ForegroundColor = progressColor;
                Console.Write(new string(chars.FilledChar, fullFilledChars));

                if (partialLevel > 0 && fullFilledChars < barWidth)
                {
                    Console.Write(chars.GradientChars[partialLevel]);
                }

                Console.ForegroundColor = backgroundColor;
                if (emptyChars > 0)
                {
                    Console.Write(new string(chars.EmptyChar, emptyChars));
                }
            }
            else if (chars.HeadChar.HasValue)
            {
                // 箭头样式模式
                int filledWidth = (int)(percentage * barWidth);
                int emptyWidth = barWidth - filledWidth;

                Console.ForegroundColor = progressColor;
                if (filledWidth > 0)
                {
                    Console.Write(new string(chars.FilledChar, filledWidth - 1));
                    Console.Write(chars.HeadChar.Value);
                }

                Console.ForegroundColor = backgroundColor;
                Console.Write(new string(chars.EmptyChar, emptyWidth));
            }
            else
            {
                // 标准模式
                int effectiveWidth = chars.IsDoubleWidth ? barWidth / 2 : barWidth;
                int filledWidth = (int)(percentage * effectiveWidth);
                int emptyWidth = effectiveWidth - filledWidth;

                Console.ForegroundColor = progressColor;
                Console.Write(new string(chars.FilledChar, filledWidth));

                Console.ForegroundColor = backgroundColor;
                Console.Write(new string(chars.EmptyChar, emptyWidth));
            }
        }

        /// <summary>
        /// 生成进度详情字符串（静态方法供复用）
        /// </summary>
        internal static string BuildProgressDetails(long current, long total, DateTime startTime, double percentage, string message = null, bool includeMessage = true)
        {
            TimeSpan elapsed = DateTime.Now - startTime;
            string elapsedStr = FormatTimeSpan(elapsed);

            string etaStr = "Calcing...";
            if (current > 0 && percentage > 0 && percentage < 1)
            {
                double remainingRatio = (1 - percentage) / percentage;
                TimeSpan eta = TimeSpan.FromTicks((long)(elapsed.Ticks * remainingRatio));
                etaStr = FormatTimeSpan(eta);
            }
            else if (percentage >= 1)
            {
                etaStr = "Completed";
            }

            double speed = elapsed.TotalSeconds > 0 ? current / elapsed.TotalSeconds : 0;
            string speedStr = speed >= 1 ? $"{speed:F1}/s" : $"{speed * 60:F1}/min";

            string details = $"{current:N0}/{total:N0} | {elapsedStr} < {etaStr} | {speedStr}";

            if (includeMessage && !string.IsNullOrEmpty(message))
            {
                details = $"{message} | {details}";
            }

            return details;
        }

        /// <summary>
        /// 渲染进度条内容（支持多种样式）
        /// </summary>
        private void RenderProgressBarContent(double percentage)
        {
            RenderProgressBarContentStatic(_chars, percentage, _totalWidth, _progressColor, _backgroundColor);
        }

        /// <summary>
        /// 安全地输出一行日志
        /// 日志会输出在进度条上方，同时进度条会重新绘制在最底部，不会破坏 UI 显示
        /// </summary>
        /// <param name="message">要输出的日志信息</param>
        /// <param name="color">可选的文字颜色</param>
        public void WriteLine(string message, ConsoleColor? color = null)
        {
            lock (_lock)
            {
                try
                {
                    // 1. 擦除旧的进度条
                    if (_fixedRow >= 0 && _fixedRow < Console.BufferHeight)
                    {
                        Console.SetCursorPosition(0, _fixedRow);
                        Console.Write(new string(' ', Console.WindowWidth - 1));
                        if (_fixedRow + 1 < Console.BufferHeight)
                        {
                            Console.SetCursorPosition(0, _fixedRow + 1);
                            Console.Write(new string(' ', Console.WindowWidth - 1));
                        }
                    }

                    // 2. 将光标移回进度条原来的位置并输出日志
                    Console.SetCursorPosition(0, Math.Max(0, _fixedRow));

                    var originalColor = Console.ForegroundColor;
                    if (color.HasValue)
                        Console.ForegroundColor = color.Value;

                    Console.WriteLine(message);

                    if (color.HasValue)
                        Console.ForegroundColor = originalColor;

                    // 3. 更新进度条的新位置（紧跟在日志后面）
                    _fixedRow = Console.CursorTop;

                    // 4. 重新为进度条预留空间
                    int oldTop = Console.CursorTop;
                    Console.WriteLine();
                    if (Console.CursorTop == oldTop) _fixedRow--;

                    oldTop = Console.CursorTop;
                    Console.WriteLine();
                    if (Console.CursorTop == oldTop) _fixedRow--;

                    // 5. 强制重绘进度条
                    _lastRenderPercentage = -1; 
                    Render();
                }
                catch
                {
                    // 忽略异常
                }
            }
        }

        /// <summary>
        /// 完成进度条，将进度设为100%
        /// </summary>
        /// <param name="message">完成消息</param>
        public void Complete(string message = null)
        {
            Update(_total, message ?? "Completed");
        }

        /// <summary>
        /// 释放资源，清理进度条显示
        /// </summary>
        public void Dispose()
        {
            lock (_lock)
            {
                try
                {
                    // 确保最终渲染一次
                    if (!_isCompleted)
                    {
                        Render();
                    }

                    // 移动光标到进度条下方
                    Console.SetCursorPosition(0, Math.Max(Console.CursorTop, _fixedRow + 2));
                }
                catch
                {
                    // 忽略异常
                }
            }
        }
    }
}
