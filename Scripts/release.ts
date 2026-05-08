import { initScrobbleTwoStep } from './shared/scrobbleTwoStep';

declare const $: any;

interface RandomReleaseChoice {
    releaseId: number;
    album: string;
    coverUrl?: string | null;
}

function isRandomReleaseChoice(value: unknown): value is RandomReleaseChoice {
    if (!value || typeof value !== 'object') return false;

    const choice = value as RandomReleaseChoice;
    return typeof choice.releaseId === 'number' && typeof choice.album === 'string';
}

function renderDiceChoices(
    diceGrid: any,
    choices: RandomReleaseChoice[],
    fallbackCoverUrl: string,
): void {
    diceGrid.empty();

    choices.forEach((choice, index) => {
        $('<a>')
            .attr({
                href: `/release/${choice.releaseId}`,
                'aria-label': `Open ${choice.album}`,
            })
            .addClass(`random-dice-choice random-dice-choice-${index + 1}`)
            .append(
                $('<img>')
                    .attr({
                        src: (choice.coverUrl ?? '').trim() || fallbackCoverUrl,
                        alt: choice.album,
                        loading: 'lazy',
                        decoding: 'async',
                    })
                    .addClass('random-dice-cover')
            )
            .appendTo(diceGrid);
    });
}

function initRandomReleaseDice(): void {
    const diceContainer = $('[data-random-dice]');
    const rollDiceButton = $('[data-random-dice-button]');
    const randomReleaseCard = $('[data-random-release-card]');
    const randomReleaseLink = $('[data-random-release-link]');

    if (!diceContainer.length || !rollDiceButton.length || !randomReleaseCard.length) return;

    const diceGrid = diceContainer.find('[data-random-dice-grid]');
    const noChoicesMessage = diceContainer.find('[data-random-dice-empty]');
    const diceEndpointUrl = diceContainer.data('random-dice-url') ?? '';
    const fallbackCoverUrl = diceContainer.data('cover-fallback') ?? '';

    if (!diceGrid.length || !noChoicesMessage.length || !diceEndpointUrl) return;

    randomReleaseLink.on('click', () => {
        diceContainer.addClass('d-none');
        randomReleaseCard.removeClass('d-none');
    });

    rollDiceButton.on('click', () => {
        rollDiceButton.prop('disabled', true);

        $.ajax({
            url: diceEndpointUrl,
            dataType: 'json',
            headers: { Accept: 'application/json' },
        })
            .done((responseData: unknown) => {
                const choices = Array.isArray(responseData) ? responseData.filter(isRandomReleaseChoice) : [];

                renderDiceChoices(diceGrid, choices, fallbackCoverUrl);
                randomReleaseCard.addClass('d-none');
                diceContainer.removeClass('d-none');
                rollDiceButton.text('Roll again');
                noChoicesMessage.toggleClass('d-none', choices.length > 0);
            })
            .fail(() => {
                renderDiceChoices(diceGrid, [], fallbackCoverUrl);
                randomReleaseCard.addClass('d-none');
                diceContainer.removeClass('d-none');
                noChoicesMessage.removeClass('d-none');
            })
            .always(() => {
                rollDiceButton.prop('disabled', false);
            });
    });
}

initScrobbleTwoStep('/Release/Scrobble');
initRandomReleaseDice();
