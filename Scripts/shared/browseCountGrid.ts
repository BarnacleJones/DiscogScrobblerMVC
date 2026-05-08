import { bindClearFilterButton, applySearchFromQueryString } from './dataTableSearch';
import { escapeAttr, escapeHtml } from './htmlEscape';

declare const $: any;

export interface BrowseCountGridRow {
    id: number;
    name: string;
    releaseCount: number;
}

export interface BrowseCountGridConfig {
    tableSelector: string;
    ajaxUrl: string;
    clearButtonSelector: string;
    searchPlaceholder: string;
    nameHref: (row: BrowseCountGridRow) => string;
}

function formatNameHref(config: BrowseCountGridConfig): (data: string, type: string, row: BrowseCountGridRow) => string {
    return (_data: string, type: string, row: BrowseCountGridRow) => {
        if (type === 'sort' || type === 'filter' || type === 'type') return row.name ?? '';
        const name = (row.name ?? '').trim();
        if (!name) return '—';
        const href = config.nameHref(row);
        return `<a href="${escapeAttr(href)}">${escapeHtml(name)}</a>`;
    };
}

function formatReleaseCount(_data: number, type: string): string | number {
    if (type === 'sort' || type === 'filter' || type === 'type') return _data ?? 0;
    return _data ?? 0;
}

export function bindBrowseCountGrid(config: BrowseCountGridConfig): void {
    const table = $(config.tableSelector).DataTable({
        ajax: {
            url: config.ajaxUrl,
            type: 'GET',
            dataSrc: '',
        },
        columns: [
            { data: 'name', render: formatNameHref(config), defaultContent: '' },
            { data: 'releaseCount', type: 'num', render: formatReleaseCount, searchable: false, className: 'text-end' },
        ],
        order: [[0, 'asc']],
        pageLength: 25,
        language: {
            search: '',
            searchPlaceholder: config.searchPlaceholder,
            lengthMenu: 'show _MENU_',
            info: '_START_–_END_ of _TOTAL_',
            paginate: { previous: '←', next: '→' },
        },
        responsive: true,
    });

    applySearchFromQueryString(table);
    bindClearFilterButton(table, config.clearButtonSelector);
}
