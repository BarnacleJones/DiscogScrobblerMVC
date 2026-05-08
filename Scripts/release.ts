import { initScrobbleTwoStep } from './shared/scrobbleTwoStep';

declare const $: any;

interface RandomReleaseChoice {
    releaseId: number;
    album: string;
    coverUrl?: string | null;
}

interface RandomDiceResponse {
    diceFace: number;
    choices: RandomReleaseChoice[];
}

const pipGridAreasByFace: Record<number, readonly string[]> = {
    1: ['2 / 2'],
    2: ['1 / 1', '3 / 3'],
    3: ['1 / 1', '2 / 2', '3 / 3'],
    4: ['1 / 1', '1 / 3', '3 / 1', '3 / 3'],
    5: ['1 / 1', '1 / 3', '2 / 2', '3 / 1', '3 / 3'],
    6: ['1 / 1', '1 / 3', '2 / 1', '2 / 3', '3 / 1', '3 / 3'],
};

function isRandomReleaseChoice(value: unknown): value is RandomReleaseChoice {
    if (!value || typeof value !== 'object') return false;

    const choice = value as RandomReleaseChoice;
    return typeof choice.releaseId === 'number' && typeof choice.album === 'string';
}

function parseRandomDiceResponse(raw: unknown): RandomDiceResponse | null {
    if (!raw || typeof raw !== 'object' || Array.isArray(raw)) return null;

    const body = raw as Record<string, unknown>;
    const face = body.face;
    const choicesRaw = body.choices;

    if (typeof face !== 'number' || face < 1 || face > 6) return null;
    if (!Array.isArray(choicesRaw)) return null;

    const choices = choicesRaw.filter(isRandomReleaseChoice);
    return { diceFace: face, choices };
}

function setDiceGridRolling(diceGrid: any, isRolling: boolean): void {
    diceGrid.toggleClass('random-dice-grid--rolling', isRolling);
}

function renderDiceChoicesOnFace(
    diceGrid: any,
    diceFace: number,
    choices: RandomReleaseChoice[],
    fallbackCoverUrl: string,
): void {
    diceGrid.empty();

    const gridAreas = pipGridAreasByFace[diceFace];
    if (!gridAreas) return;

    const count = Math.min(choices.length, gridAreas.length);
    for (let index = 0; index < count; index++) {
        const choice = choices[index];
        const gridArea = gridAreas[index];

        $('<a>')
            .attr({
                href: `/release/${choice.releaseId}`,
                'aria-label': `Open ${choice.album}`,
            })
            .addClass('random-dice-choice')
            .css('grid-area', gridArea)
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
    }
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
        setDiceGridRolling(diceGrid, true);

        $.ajax({
            url: diceEndpointUrl,
            dataType: 'json',
            headers: { Accept: 'application/json' },
        })
            .done((responseData: unknown) => {
                const parsed = parseRandomDiceResponse(responseData);
                if (!parsed) {
                    renderDiceChoicesOnFace(diceGrid, 1, [], fallbackCoverUrl);
                    randomReleaseCard.addClass('d-none');
                    diceContainer.removeClass('d-none');
                    rollDiceButton.text('Roll again');
                    noChoicesMessage.removeClass('d-none');
                    return;
                }

                const { diceFace, choices } = parsed;
                renderDiceChoicesOnFace(diceGrid, diceFace, choices, fallbackCoverUrl);
                randomReleaseCard.addClass('d-none');
                diceContainer.removeClass('d-none');
                rollDiceButton.text('Roll again');
                noChoicesMessage.toggleClass('d-none', choices.length > 0);
            })
            .fail(() => {
                renderDiceChoicesOnFace(diceGrid, 1, [], fallbackCoverUrl);
                randomReleaseCard.addClass('d-none');
                diceContainer.removeClass('d-none');
                rollDiceButton.text('Roll again');
                noChoicesMessage.removeClass('d-none');
            })
            .always(() => {
                setDiceGridRolling(diceGrid, false);
                rollDiceButton.prop('disabled', false);
            });
    });
}

initScrobbleTwoStep('/Release/Scrobble');
initRandomReleaseDice();
