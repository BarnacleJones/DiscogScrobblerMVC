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
  var pipGridAreasByFace = {
    1: ["2 / 2"],
    2: ["1 / 1", "3 / 3"],
    3: ["1 / 1", "2 / 2", "3 / 3"],
    4: ["1 / 1", "1 / 3", "3 / 1", "3 / 3"],
    5: ["1 / 1", "1 / 3", "2 / 2", "3 / 1", "3 / 3"],
    6: ["1 / 1", "1 / 3", "2 / 1", "2 / 3", "3 / 1", "3 / 3"]
  };
  function isRandomReleaseChoice(value) {
    if (!value || typeof value !== "object") return false;
    const choice = value;
    return typeof choice.releaseId === "number" && typeof choice.album === "string";
  }
  function parseRandomDiceResponse(raw) {
    if (!raw || typeof raw !== "object" || Array.isArray(raw)) return null;
    const body = raw;
    const face = body.face;
    const choicesRaw = body.choices;
    if (typeof face !== "number" || face < 1 || face > 6) return null;
    if (!Array.isArray(choicesRaw)) return null;
    const choices = choicesRaw.filter(isRandomReleaseChoice);
    return { diceFace: face, choices };
  }
  function setDiceGridRolling(diceGrid, isRolling) {
    diceGrid.toggleClass("random-dice-grid--rolling", isRolling);
  }
  function renderDiceChoicesOnFace(diceGrid, diceFace, choices, fallbackCoverUrl) {
    diceGrid.empty();
    const gridAreas = pipGridAreasByFace[diceFace];
    if (!gridAreas) return;
    const count = Math.min(choices.length, gridAreas.length);
    for (let index = 0; index < count; index++) {
      const choice = choices[index];
      const gridArea = gridAreas[index];
      const coverSrc = (choice.coverUrl ?? "").trim() || fallbackCoverUrl;
      const coverImg = $("<img>").attr({
        src: coverSrc,
        alt: "",
        loading: "lazy",
        decoding: "async"
      }).addClass("random-dice-cover");
      if (fallbackCoverUrl) {
        coverImg.on("error", function() {
          this.onerror = null;
          this.src = fallbackCoverUrl;
        });
      }
      $("<a>").attr({
        href: `/release/${choice.releaseId}`,
        "aria-label": `Open ${choice.album}`
      }).addClass("random-dice-choice").css("grid-area", gridArea).append(coverImg).appendTo(diceGrid);
    }
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
      setDiceGridRolling(diceGrid, true);
      $.ajax({
        url: diceEndpointUrl,
        dataType: "json",
        headers: { Accept: "application/json" }
      }).done((responseData) => {
        const parsed = parseRandomDiceResponse(responseData);
        if (!parsed) {
          renderDiceChoicesOnFace(diceGrid, 1, [], fallbackCoverUrl);
          randomReleaseCard.addClass("d-none");
          diceContainer.removeClass("d-none");
          rollDiceButton.text("Roll again");
          noChoicesMessage.removeClass("d-none");
          return;
        }
        const { diceFace, choices } = parsed;
        renderDiceChoicesOnFace(diceGrid, diceFace, choices, fallbackCoverUrl);
        randomReleaseCard.addClass("d-none");
        diceContainer.removeClass("d-none");
        rollDiceButton.text("Roll again");
        noChoicesMessage.toggleClass("d-none", choices.length > 0);
      }).fail(() => {
        renderDiceChoicesOnFace(diceGrid, 1, [], fallbackCoverUrl);
        randomReleaseCard.addClass("d-none");
        diceContainer.removeClass("d-none");
        rollDiceButton.text("Roll again");
        noChoicesMessage.removeClass("d-none");
      }).always(() => {
        setDiceGridRolling(diceGrid, false);
        rollDiceButton.prop("disabled", false);
      });
    });
  }
  initScrobbleTwoStep("/Release/Scrobble");
  initRandomReleaseDice();
})();
