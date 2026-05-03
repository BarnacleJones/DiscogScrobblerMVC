declare const $: any;

$(document).ready(function () {
    $('.scrobble-btn').on('click', function (this: HTMLElement) {
        const btn = $(this);
        const releaseId = btn.data('release-id') as number;

        btn.text('Scrobbling…').prop('disabled', true);

        $.post('/Release/Scrobble', { releaseId })
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
