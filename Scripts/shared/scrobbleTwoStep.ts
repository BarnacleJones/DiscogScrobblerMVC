declare const $: any;

const confirmPromptHtml =
    '<span class="d-inline-flex align-items-center justify-content-center gap-1">' +
    '<i class="bi bi-check-lg" aria-hidden="true"></i>' +
    '<span>Confirm scrobble</span></span>';

function submitScrobble(scrobbleButton: any, releaseId: number, postUrl: string): void {
    scrobbleButton.prop('disabled', true).text('Scrobbling…');

    $.post(postUrl, { releaseId })
        .done(() => {
            scrobbleButton.removeData('confirm-pending');
            scrobbleButton.text('✓ Scrobbled')
                .removeClass('btn-primary btn-warning')
                .addClass('btn-outline-secondary');
        })
        .fail(() => {
            scrobbleButton.removeData('confirm-pending');
            scrobbleButton.text('Failed')
                .removeClass('btn-primary btn-warning')
                .addClass('btn-outline-secondary')
                .prop('disabled', false);
        });
}

export function initScrobbleTwoStep(postUrl: string): void {
    $(function () {
        $('.scrobble-btn').on('click', function (this: HTMLElement) {
            const scrobbleButton = $(this);
            const releaseId = scrobbleButton.data('release-id') as number;

            if (scrobbleButton.prop('disabled')) {
                return;
            }

            if (!scrobbleButton.data('confirm-pending')) {
                scrobbleButton.data('confirm-pending', true);
                scrobbleButton.removeClass('btn-primary').addClass('btn-warning');
                scrobbleButton.html(confirmPromptHtml);
                return;
            }

            submitScrobble(scrobbleButton, releaseId, postUrl);
        });
    });
}
