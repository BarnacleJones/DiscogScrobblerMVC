declare const $: any;

const COVERS_PER_ROLL = 6;

function pickRandomCovers(allCovers: string[], count: number): string[] {
    const remaining = [...allCovers];
    const picks: string[] = [];

    while (remaining.length > 0 && picks.length < count) {
        const pickIndex = Math.floor(Math.random() * remaining.length);
        picks.push(remaining[pickIndex]);
        remaining.splice(pickIndex, 1);
    }

    return picks;
}

function renderCoverTiles(coverGrid: any, coverUrls: string[], fallbackCoverUrl: string): void {
    coverGrid.empty();

    coverUrls.forEach((coverUrl) => {
        $('<img>')
            .attr({
                src: (coverUrl ?? '').trim() || fallbackCoverUrl,
                alt: 'Random album cover',
                loading: 'lazy',
                decoding: 'async',
            })
            .addClass('lastfm-cover-tile')
            .appendTo(coverGrid);
    });
}

function parseCoverCandidates(rawJson: string | undefined): string[] {
    try {
        const parsed = JSON.parse(rawJson ?? '[]');
        if (!Array.isArray(parsed)) return [];
        return parsed.filter((url): url is string => typeof url === 'string' && url.trim().length > 0);
    } catch {
        return [];
    }
}

function initCoverRoll(): void {
    const coverRoll = $('[data-lastfm-cover-roll]');
    if (!coverRoll.length) return;

    const coverGrid = coverRoll.find('[data-lastfm-cover-grid]');
    const rerollButton = coverRoll.find('[data-lastfm-reroll]');
    if (!coverGrid.length || !rerollButton.length) return;

    const candidateCovers = parseCoverCandidates(coverRoll.attr('data-cover-candidates'));
    if (candidateCovers.length === 0) return;

    const fallbackCoverUrl = coverRoll.attr('data-cover-fallback') ?? '';

    rerollButton.on('click', () => {
        renderCoverTiles(coverGrid, pickRandomCovers(candidateCovers, COVERS_PER_ROLL), fallbackCoverUrl);
    });
}

$(initCoverRoll);
