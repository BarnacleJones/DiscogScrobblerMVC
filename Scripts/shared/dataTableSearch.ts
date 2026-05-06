declare const $: any;

export function applySearchFromQueryString(table: DataTables.Api, queryKey = 'q'): void {
    const queryParams = new URLSearchParams(window.location.search);
    const searchQuery = queryParams.get(queryKey);
    if (searchQuery && searchQuery.trim()) {
        table.search(searchQuery.trim()).draw();
    }
}

export function bindClearFilterButton(
    table: DataTables.Api,
    clearButtonSelector: string,
    queryKey = 'q'
): void {
    const clearButton = $(clearButtonSelector);

    const updateClearButtonState = (): void => {
        const hasActiveSearch = (table.search() ?? '').trim().length > 0;
        clearButton.prop('disabled', !hasActiveSearch);
    };

    clearButton.on('click', function () {
        table.search('').draw();
        const currentUrl = new URL(window.location.href);
        currentUrl.searchParams.delete(queryKey);
        window.history.replaceState({}, '', currentUrl.toString());
        updateClearButtonState();
    });

    table.on('search.dt', function () {
        updateClearButtonState();
    });

    updateClearButtonState();
}
