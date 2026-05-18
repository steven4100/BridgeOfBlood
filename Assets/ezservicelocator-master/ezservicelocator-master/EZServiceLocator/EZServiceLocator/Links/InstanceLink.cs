namespace EZServiceLocation
{
    internal class InstanceLink<TInterface> : GenericLink<TInterface>
    {
        internal InstanceLink(TInterface instance)
        {
            _serviceInstance = instance;
        }

        internal override TInterface Invoke(bool requiresNew = false, object[] parameters = null)
            => _serviceInstance;

        internal override object InvokeObject(bool requiresNew = false, object[] parameters = null)
            => _serviceInstance;
    }
}
