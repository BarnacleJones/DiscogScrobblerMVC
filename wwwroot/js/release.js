"use strict";
(() => {
  // Scripts/shared/scrobbleTwoStep.ts
  var confirmPromptHtml = '<span class="d-inline-flex align-items-center justify-content-center gap-1"><i class="bi bi-check-lg" aria-hidden="true"></i><span>Confirm scrobble</span></span>';
  function submitScrobble(scrobbleButton, releaseId, postUrl) {
    scrobbleButton.prop("disabled", true).text("Scrobbling\u2026");
    $.post(postUrl, { releaseId }).done(() => {
      scrobbleButton.removeData("confirm-pending");
      scrobbleButton.text("\u2713 Scrobbled").removeClass("btn-primary btn-warning").addClass("btn-outline-secondary");
    }).fail(() => {
      scrobbleButton.removeData("confirm-pending");
      scrobbleButton.text("Failed").removeClass("btn-primary btn-warning").addClass("btn-outline-secondary").prop("disabled", false);
    });
  }
  function initScrobbleTwoStep(postUrl) {
    $(function() {
      $(".scrobble-btn").on("click", function() {
        const scrobbleButton = $(this);
        const releaseId = scrobbleButton.data("release-id");
        if (scrobbleButton.prop("disabled")) {
          return;
        }
        if (!scrobbleButton.data("confirm-pending")) {
          scrobbleButton.data("confirm-pending", true);
          scrobbleButton.removeClass("btn-primary").addClass("btn-warning");
          scrobbleButton.html(confirmPromptHtml);
          return;
        }
        submitScrobble(scrobbleButton, releaseId, postUrl);
      });
    });
  }

  // Scripts/release.ts
  function isRandomReleaseChoice(value) {
    if (!value || typeof value !== "object") return false;
    const choice = value;
    return typeof choice.releaseId === "number" && typeof choice.album === "string";
  }
  function renderDiceChoices(diceGrid, choices, fallbackCoverUrl) {
    diceGrid.empty();
    choices.forEach((choice, index) => {
      $("<a>").attr({
        href: `/release/${choice.releaseId}`,
        "aria-label": `Open ${choice.album}`
      }).addClass(`random-dice-choice random-dice-choice-${index + 1}`).append(
        $("<img>").attr({
          src: (choice.coverUrl ?? "").trim() || fallbackCoverUrl,
          alt: choice.album,
          loading: "lazy",
          decoding: "async"
        }).addClass("random-dice-cover")
      ).appendTo(diceGrid);
    });
  }
  function initRandomReleaseDice() {
    const diceContainer = $("[data-random-dice]");
    const rollDiceButton = $("[data-random-dice-button]");
    const randomReleaseCard = $("[data-random-release-card]");
    const randomReleaseLink = $("[data-random-release-link]");
    if (!diceContainer.length || !rollDiceButton.length || !randomReleaseCard.length) return;
    const diceGrid = diceContainer.find("[data-random-dice-grid]");
    const noChoicesMessage = diceContainer.find("[data-random-dice-empty]");
    const diceEndpointUrl = diceContainer.data("random-dice-url") ?? "";
    const fallbackCoverUrl = diceContainer.data("cover-fallback") ?? "";
    if (!diceGrid.length || !noChoicesMessage.length || !diceEndpointUrl) return;
    randomReleaseLink.on("click", () => {
      diceContainer.addClass("d-none");
      randomReleaseCard.removeClass("d-none");
    });
    rollDiceButton.on("click", () => {
      rollDiceButton.prop("disabled", true);
      $.ajax({
        url: diceEndpointUrl,
        dataType: "json",
        headers: { Accept: "application/json" }
      }).done((responseData) => {
        const choices = Array.isArray(responseData) ? responseData.filter(isRandomReleaseChoice) : [];
        renderDiceChoices(diceGrid, choices, fallbackCoverUrl);
        randomReleaseCard.addClass("d-none");
        diceContainer.removeClass("d-none");
        rollDiceButton.text("Roll again");
        noChoicesMessage.toggleClass("d-none", choices.length > 0);
      }).fail(() => {
        renderDiceChoices(diceGrid, [], fallbackCoverUrl);
        randomReleaseCard.addClass("d-none");
        diceContainer.removeClass("d-none");
        noChoicesMessage.removeClass("d-none");
      }).always(() => {
        rollDiceButton.prop("disabled", false);
      });
    });
  }
  initScrobbleTwoStep("/Release/Scrobble");
  initRandomReleaseDice();
})();
