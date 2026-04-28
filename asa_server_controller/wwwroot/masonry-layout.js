const observers = new WeakMap();

export function observeMasonryLayout(element, dotNetRef) {
    if (!element || !dotNetRef) {
        return;
    }

    const notify = () => {
        const width = element.getBoundingClientRect().width || element.clientWidth || 0;
        dotNetRef.invokeMethodAsync("OnContainerWidthChanged", width);
    };

    const observer = new ResizeObserver(() => {
        notify();
    });

    observer.observe(element);
    observers.set(element, observer);
    notify();
}

export function disposeMasonryLayout(element) {
    const observer = observers.get(element);
    if (!observer) {
        return;
    }

    observer.disconnect();
    observers.delete(element);
}
