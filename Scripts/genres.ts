import { bindBrowseCountGrid } from './shared/browseCountGrid';

declare const $: any;

$(document).ready(function () {
    bindBrowseCountGrid({
        tableSelector: '#genresTable',
        ajaxUrl: '/Genres/GetGenres',
        clearButtonSelector: '#clearGenresFilterBtn',
        searchPlaceholder: 'search genres…',
        nameHref: (row) => `/collection/genre/${row.id}`,
    });
});
