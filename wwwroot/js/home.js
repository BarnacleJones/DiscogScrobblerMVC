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

  // Scripts/home.ts
  initScrobbleTwoStep("/Collection/Scrobble");
})();
