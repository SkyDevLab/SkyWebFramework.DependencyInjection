namespace SkyWebFramework.DependencyInjection
{
    /// <summary>
    /// Defines behavior when a service registration conflict is encountered.
    /// </summary>
    public enum DuplicateRegistrationBehavior
    {
        /// <summary>
        /// Keeps the existing registration and skips the new one.
        /// </summary>
        Skip,

        /// <summary>
        /// Removes existing registration(s) for the service type and adds the new one.
        /// </summary>
        Replace,

        /// <summary>
        /// Throws an <see cref="System.InvalidOperationException"/> when duplicate registration is encountered.
        /// </summary>
        Throw,

        /// <summary>
        /// Appends the new registration alongside existing registrations.
        /// </summary>
        Append
    }
}
