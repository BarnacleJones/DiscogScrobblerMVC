"use strict";
(() => {
  // Scripts/home.ts
  $(document).ready(function() {
    $(".scrobble-btn").on("click", function() {
      const btn = $(this);
      const releaseId = btn.data("release-id");
      btn.text("Scrobbling\u2026").prop("disabled", true);
      $.post("/Collection/Scrobble", { releaseId }).done(() => {
        btn.text("\u2713 Scrobbled").addClass("btn-outline-secondary").removeClass("btn-primary");
      }).fail(() => {
        btn.text("Failed").addClass("btn-outline-secondary").removeClass("btn-primary").prop("disabled", false);
      });
    });
  });
})();
