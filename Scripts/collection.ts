import { bindClearFilterButton, applySearchFromQueryString } from './shared/dataTableSearch';
import { escapeAttr, escapeHtml } from './shared/htmlEscape';

// DataTables and jQuery are loaded globally via CDN/Bootstrap
// Declare them so TypeScript doesn't complain
declare const $: any;

const collectionCoverFallback = '/images/placeholder-cover.svg';

interface CollectionArtistLink {
    id: number;
    name: string;
}

interface CollectionItem {
    releaseId: number;
    discogsMasterId?: number | null;
    artistDisplay: string;
    artists: CollectionArtistLink[];
    album: string;
    year: number | null;
    dateAdded: string;
    coverUrl?: string | null;
}

function formatDate(iso: string): string {
    if (!iso) return '—';
    return new Date(iso).toLocaleDateString('en-NZ', {
        day: 'numeric',
        month: 'short',
        year: 'numeric'
    });
}

function formatArtistLinks(data: string, type: string, row: CollectionItem): string {
    if (type === 'sort' || type === 'filter' || type === 'type') return data;
    const artists = row.artists;
    if (!artists || artists.length === 0) return '—';
    return artists
        .map((artist) => `<a href="/artist/${artist.id}">${escapeHtml(artist.name)}</a>`)
        .join(', ');
}

function formatAlbumLink(data: string, type: string, row: CollectionItem): string {
    if (type === 'sort' || type === 'filter' || type === 'type') return data;
    return `<a href="/release/${row.releaseId}">${escapeHtml(data)}</a>`;
}

function formatYearLink(data: number | null, type: string): string {
    if (type === 'sort' || type === 'filter' || type === 'type') return data == null ? '' : String(data);
    if (data == null) return '—';
    const year = String(data);
    return `<a href="/Collection?q=${encodeURIComponent(year)}">${escapeHtml(year)}</a>`;
}

function formatCoverThumb(_data: unknown, type: string, row: CollectionItem): string {
    if (type === 'sort' || type === 'filter' || type === 'type') return '';
    const trimmed = row.coverUrl != null ? String(row.coverUrl).trim() : '';
    const coverSrc = trimmed || collectionCoverFallback;
    const safeCoverUrl = escapeAttr(coverSrc);
    return `<a href="/release/${row.releaseId}" class="collection-cover-link" tabindex="-1" aria-hidden="true"><img class="collection-cover-thumb" src="${safeCoverUrl}" alt="" loading="lazy" width="40" height="40" onerror="this.onerror=null;this.src='${collectionCoverFallback}'" /></a>`;
}

function expandedRowHtml(item: CollectionItem): string {
    const releaseUrl = `https://www.discogs.com/release/${item.releaseId}`;
    const discogsLinks = [
        `<a href="${escapeAttr(releaseUrl)}" target="_blank" rel="noopener noreferrer">Release</a>`
    ];

    if (item.discogsMasterId != null) {
        const masterUrl = `https://www.discogs.com/master/${item.discogsMasterId}`;
        discogsLinks.push(`<a href="${escapeAttr(masterUrl)}" target="_blank" rel="noopener noreferrer">Master</a>`);
    }

    return `
        <div class="child-content">
            <div>
                <div class="child-label">Date added</div>
                <div class="child-value">${formatDate(item.dateAdded)}</div>
            </div>
            <div>
                <div class="child-label">Discogs</div>
                <div class="child-value">${discogsLinks.join(' <span class="text-secondary">·</span> ')}</div>
            </div>
            <div class="ms-auto">
                <button class="btn btn-primary btn-sm scrobble-btn"
                        data-release-id="${item.releaseId}"
                        data-artist="${escapeAttr(item.artistDisplay)}"
                        data-album="${escapeAttr(item.album)}">
                    Scrobble
                </button>
            </div>
        </div>`;
}

$(document).ready(function () {

    const collectionTable = $('#collectionTable').DataTable({
        ajax: {
            url: '/Collection/GetCollection',
            type: 'GET',
            dataSrc: ''
        },
        columns: [
            { className: 'dt-control', orderable: false, data: null, defaultContent: '' },
            {
                className: 'collection-cover-cell',
                orderable: false,
                searchable: false,
                data: 'coverUrl',
                render: formatCoverThumb
            },
            { data: 'artistDisplay', render: formatArtistLinks },
            { data: 'album', render: formatAlbumLink },
            { data: 'year', render: formatYearLink }
        ],
        order: [[2, 'asc']],
        pageLength: 25,
        language: {
            search: '',
            searchPlaceholder: 'search collection…',
            lengthMenu: 'show _MENU_',
            info: '_START_–_END_ of _TOTAL_',
            paginate: { previous: '←', next: '→' }
        },
        responsive: true
    });

    applySearchFromQueryString(collectionTable);
    bindClearFilterButton(collectionTable, '#clearCollectionFilterBtn');

    $('#collectionTable tbody').on('click', 'td.dt-control', function (this: HTMLElement) {
        const clickedRow = $(this).closest('tr');
        const dataRow = collectionTable.row(clickedRow);

        if (dataRow.child.isShown()) {
            dataRow.child.hide();
            clickedRow.removeClass('dt-hasChild');
        } else {
            dataRow.child(expandedRowHtml(dataRow.data() as CollectionItem)).show();
            clickedRow.addClass('dt-hasChild');
            clickedRow.next().addClass('child-row');
        }
    });

    $('#collectionTable tbody').on('click', '.scrobble-btn', function (this: HTMLElement) {
        const scrobbleButton = $(this);
        const releaseId = scrobbleButton.data('release-id') as number;

        scrobbleButton.text('Scrobbling…').prop('disabled', true);

        $.post('/Collection/Scrobble', { releaseId })
            .done(() => {
                scrobbleButton.text('✓ Scrobbled')
                    .addClass('btn-outline-secondary')
                    .removeClass('btn-primary');
            })
            .fail(() => {
                scrobbleButton.text('Failed')
                    .addClass('btn-outline-secondary')
                    .removeClass('btn-primary')
                    .prop('disabled', false);
            });
    });
});
