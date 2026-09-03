namespace CoffeeBean
{
    /// <summary>
    /// 导出定制步骤：一个步骤负责一类注入（改配置或落盘）。
    /// 约定：自定义步骤改 <see cref="CExportSession.Config"/>（Order 小）；
    /// 内置落盘步骤读最终 Config 写文件（Order 大，恒在最后）。
    /// </summary>
    public interface IExportStep
    {
        /// <summary>唯一 id（日志/幂等/异常定位）。</summary>
        string Id { get; }

        /// <summary>执行顺序（小先执行；落盘步骤建议 10000）。</summary>
        int Order { get; }

        /// <summary>该步骤是否在当前会话激活（按平台/配置开关）。</summary>
        bool IsActive(CExportSession session);

        /// <summary>执行注入；失败抛 <see cref="CExportException"/>。</summary>
        void Execute(CExportSession session);
    }
}
