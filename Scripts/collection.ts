// DataTables and jQuery are loaded globally via CDN/Bootstrap
// Declare them so TypeScript doesn't complain
declare const $: any;

interface CollectionItem {
    releaseId: number;
    artist: string;
    album: string;
    year: number;
    dateAdded: string;
}

function formatDate(iso: string): string {
    if (!iso) return '—';
    return new Date(iso).toLocaleDateString('en-NZ', {
        day: 'numeric',
        month: 'short',
        year: 'numeric'
    });
}

function childContent(data: CollectionItem): string {
    return `
        <div class="child-content">
            <div>
                <div class="child-label">Date added</div>
                <div class="child-value">${formatDate(data.dateAdded)}</div>
            </div>
            <div class="ms-auto">
                <button class="btn btn-primary btn-sm scrobble-btn"
                        data-release-id="${data.releaseId}"
                        data-artist="${data.artist}"
                        data-album="${data.album}">
                    Scrobble
                </button>
            </div>
        </div>`;
}

$(document).ready(function () {

    const table = $('#collectionTable').DataTable({
        ajax: {
            url: '/Collection/GetCollection',
            type: 'GET',
            dataSrc: ''
        },
        columns: [
            { className: 'dt-control', orderable: false, data: null, defaultContent: '' },
            { data: 'artist' },
            { data: 'album' },
            { data: 'year' }
        ],
        order: [[1, 'asc']],
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

    $('#collectionTable tbody').on('click', 'td.dt-control', function (this: HTMLElement) {
        const tr = $(this).closest('tr');
        const row = table.row(tr);

        if (row.child.isShown()) {
            row.child.hide();
            tr.removeClass('dt-hasChild');
        } else {
            row.child(childContent(row.data() as CollectionItem)).show();
            tr.addClass('dt-hasChild');
            tr.next().addClass('child-row');
        }
    });

    $('#collectionTable tbody').on('click', '.scrobble-btn', function (this: HTMLElement) {
        const btn = $(this);
        const releaseId = btn.data('release-id') as number;

        btn.text('Scrobbling…').prop('disabled', true);

        $.post('/Collection/Scrobble', { releaseId })
            .done(() => {
                btn.text('✓ Scrobbled')
                    .addClass('btn-outline-secondary')
                    .removeClass('btn-primary');
            })
            .fail(() => {
                btn.text('Failed')
                    .addClass('btn-outline-secondary')
                    .removeClass('btn-primary')
                    .prop('disabled', false);
            });
    });
});