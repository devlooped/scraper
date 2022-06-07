using System.ComponentModel;

[EditorBrowsable(EditorBrowsableState.Never)]
static class LazyServiceExtension
{
    /// <summary>
    /// Allows resolving any service lazily using <see cref="Lazy{T}"/> as 
    /// a dependency instead of the direct service type.
    /// </summary>
    public static IServiceCollection AddLazy(this IServiceCollection services)
        => services.AddTransient(typeof(Lazy<>), typeof(LazyService<>));

    class LazyService<T> : Lazy<T> where T : class
    {
        public LazyService(IServiceProvider provider)
            : base(() => provider.GetRequiredService<T>()) { }
    }
}
