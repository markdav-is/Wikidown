// Bridges browser network events into the NetworkStatus .NET service.
window.wikidownNet = (() => {
    let dotnetRef = null;
    const onChange = () => {
        if (dotnetRef) dotnetRef.invokeMethodAsync('OnNetChanged', navigator.onLine);
    };
    return {
        register(ref) {
            dotnetRef = ref;
            window.addEventListener('online', onChange);
            window.addEventListener('offline', onChange);
            onChange();
        },
        unregister() {
            window.removeEventListener('online', onChange);
            window.removeEventListener('offline', onChange);
            dotnetRef = null;
        }
    };
})();
