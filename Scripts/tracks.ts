import { bindClearFilterButton, applySearchFromQueryString } from './shared/dataTableSearch';
import { escapeAttr, escapeHtml } from './shared/htmlEscape';

// DataTables and jQuery are loaded globally via CDN/Bootstrap
// Declare them so TypeScript doesn't complain
declare const $: any;

interface TrackItem {
    releaseId: number;
    artistDisplay: string;
    album: string;
    year: number | null;
    position: string;
    title: string;
    duration?: string | null;
}

function normalizePositionForSort(rawPosition: string | null | undefined): string {
    const position = (rawPosition ?? '').trim().toUpperCase();
    // Typical Discogs positions are like "A1", "B2", "D1". Normalize to "A0001" for stable string sorting.
    const sideAndNumberMatch = /^([A-Z]+)\s*([0-9]+)$/.exec(position);
    if (!sideAndNumberMatch) return position;
    const side = sideAndNumberMatch[1];
    const trackNumber = Number.parseInt(sideAndNumberMatch[2], 10);
    const paddedTrackNumber = Number.isFinite(trackNumber)
        ? String(trackNumber).padStart(4, '0')
        : sideAndNumberMatch[2];
    return `${side}${paddedTrackNumber}`;
}

function formatPosition(data: string, type: string): string {
    const position = (data ?? '').trim();
    if (type === 'sort' || type === 'type') return normalizePositionForSort(position);
    if (type === 'filter') return '';
    if (!position) return '—';
    return escapeHtml(position);
}

function formatTitle(data: string, type: string): string {
    if (type === 'sort' || type === 'filter' || type === 'type') return data;
    const title = (data ?? '').trim();
    if (!title) return '—';
    const searchUrl = `/Tracks?q=${encodeURIComponent(title)}`;
    return `<a href="${escapeAttr(searchUrl)}">${escapeHtml(title)}</a>`;
}

function formatDuration(data: string | null | undefined, type: string): string {
    const duration = (data ?? '').trim();
    if (type === 'sort' || type === 'filter' || type === 'type') return duration;
    if (!duration) return '—';
    const searchUrl = `/Tracks?q=${encodeURIComponent(duration)}`;
    return `<a href="${escapeAttr(searchUrl)}">${escapeHtml(duration)}</a>`;
}

function formatArtist(data: string, type: string): string {
    if (type === 'sort' || type === 'filter' || type === 'type') return data;
    const artist = (data ?? '').trim();
    if (!artist) return '—';
    const searchUrl = `/Tracks?q=${encodeURIComponent(artist)}`;
    return `<a href="${escapeAttr(searchUrl)}">${escapeHtml(artist)}</a>`;
}

function expandedRowHtml(track: TrackItem): string {
    const album = escapeHtml(track.album ?? '—');
    const year = track.year == null ? '—' : escapeHtml(String(track.year));
    const releaseUrl = `/release/${track.releaseId}`;
    return `
        <div class="child-content">
            <div>
                <div class="child-label">Album</div>
                <div class="child-value"><a href="${escapeAttr(releaseUrl)}">${album}</a></div>
            </div>
            <div class="ms-4">
                <div class="child-label">Year</div>
                <div class="child-value">${year}</div>
            </div>
        </div>`;
}

$(document).ready(function () {
    const tracksTable = $('#tracksTable').DataTable({
        ajax: {
            url: '/Tracks/GetTracks',
            type: 'GET',
            dataSrc: ''
        },
        columns: [
            { className: 'dt-control', orderable: false, data: null, defaultContent: '' },
            { data: 'position', render: formatPosition, searchable: false },
            { data: 'title', render: formatTitle },
            { data: 'duration', render: formatDuration },
            { data: 'artistDisplay', render: formatArtist }
        ],
        order: [[2, 'asc']],
        pageLength: 25,
        language: {
            search: '',
            searchPlaceholder: 'search tracks…',
            lengthMenu: 'show _MENU_',
            info: '_START_–_END_ of _TOTAL_',
            paginate: { previous: '←', next: '→' }
        },
        responsive: true
    });

    applySearchFromQueryString(tracksTable);
    bindClearFilterButton(tracksTable, '#clearTracksFilterBtn');

    $('#tracksTable tbody').on('click', 'td.dt-control', function (this: HTMLElement) {
        const clickedRow = $(this).closest('tr');
        const dataRow = tracksTable.row(clickedRow);

        if (dataRow.child.isShown()) {
            dataRow.child.hide();
            clickedRow.removeClass('dt-hasChild');
        } else {
            dataRow.child(expandedRowHtml(dataRow.data() as TrackItem)).show();
            clickedRow.addClass('dt-hasChild');
            clickedRow.next().addClass('child-row');
        }
    });
});
