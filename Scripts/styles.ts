import { bindBrowseCountGrid } from './shared/browseCountGrid';

declare const $: any;

$(document).ready(function () {
    bindBrowseCountGrid({
        tableSelector: '#stylesTable',
        ajaxUrl: '/Styles/GetStyles',
        clearButtonSelector: '#clearStylesFilterBtn',
        searchPlaceholder: 'search styles…',
        nameHref: (row) => `/collection/style/${row.id}`,
    });
});
