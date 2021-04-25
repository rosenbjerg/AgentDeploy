namespace AgentDeploy.Models.Scripts
{
    public enum ScriptLockingLevel
    {
        /// <summary>
        /// No locking; concurrent invocations is fully allowed
        /// </summary>
        None,
        /// <summary>
        /// Token locking; the same script cannot be invoked with the same token concurrently
        /// </summary>
        Token,
        /// <summary>
        /// Script locking; the same script cannot be invoked concurrently
        /// </summary>
        Script
    }
}