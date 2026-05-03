"use strict";
(() => {
  // Scripts/collection.ts
  function formatDate(iso) {
    if (!iso) return "\u2014";
    return new Date(iso).toLocaleDateString("en-NZ", {
      day: "numeric",
      month: "short",
      year: "numeric"
    });
  }
  function childContent(data) {
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
  $(document).ready(function() {
    const table = $("#collectionTable").DataTable({
      ajax: {
        url: "/Collection/GetCollection",
        type: "GET",
        dataSrc: ""
      },
      columns: [
        { className: "dt-control", orderable: false, data: null, defaultContent: "" },
        { data: "artist" },
        { data: "album" },
        { data: "year" }
      ],
      order: [[1, "asc"]],
      pageLength: 25,
      language: {
        search: "",
        searchPlaceholder: "search collection\u2026",
        lengthMenu: "show _MENU_",
        info: "_START_\u2013_END_ of _TOTAL_",
        paginate: { previous: "\u2190", next: "\u2192" }
      },
      responsive: true
    });
    $("#collectionTable tbody").on("click", "td.dt-control", function() {
      const tr = $(this).closest("tr");
      const row = table.row(tr);
      if (row.child.isShown()) {
        row.child.hide();
        tr.removeClass("dt-hasChild");
      } else {
        row.child(childContent(row.data())).show();
        tr.addClass("dt-hasChild");
        tr.next().addClass("child-row");
      }
    });
    $("#collectionTable tbody").on("click", ".scrobble-btn", function() {
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
