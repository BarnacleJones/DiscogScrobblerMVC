import { bindBrowseCountGrid } from './shared/browseCountGrid';

declare const $: any;

$(document).ready(function () {
    bindBrowseCountGrid({
        tableSelector: '#labelsTable',
        ajaxUrl: '/Labels/GetLabels',
        clearButtonSelector: '#clearLabelsFilterBtn',
        searchPlaceholder: 'search labels…',
        nameHref: (row) => `/label/${row.id}`,
    });
});
