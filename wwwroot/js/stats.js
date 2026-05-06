"use strict";
(() => {
  // Scripts/stats.ts
  function readCssVar(name, fallback) {
    if (typeof window === "undefined" || !document.body) return fallback;
    const value = getComputedStyle(document.body).getPropertyValue(name).trim();
    return value.length > 0 ? value : fallback;
  }
  var accentColor = readCssVar("--accent", "#d4f542");
  var accentBorderColor = readCssVar("--accent2", "#a8c233");
  var stylesBarColor = readCssVar("--teal", "#3ecfa0");
  var genresBarColor = readCssVar("--amber", "#f5a623");
  var artistsBarColor = readCssVar("--purple", "#9b7ff4");
  var axisLabelColor = readCssVar("--txt2", "#8a897f");
  var axisTickColor = readCssVar("--txt3", "#5a5955");
  var chartGridColor = "rgba(255,255,255,0.06)";
  function readJsonAttribute(canvas, dataAttribute) {
    const raw = canvas.attr(dataAttribute);
    if (raw == null || raw.length === 0) return null;
    try {
      return JSON.parse(raw);
    } catch (parseError) {
      console.warn("stats: failed to parse", dataAttribute, parseError);
      return null;
    }
  }
  function applyChartDefaults() {
    if (typeof Chart === "undefined") return;
    Chart.defaults.color = axisLabelColor;
    Chart.defaults.font.family = "'DM Mono', monospace";
    Chart.defaults.font.size = 11;
  }
  function renderLetterDistributionChart(canvasId) {
    const canvas = $("#" + canvasId);
    if (!canvas.length) return;
    const buckets = readJsonAttribute(canvas, "data-stats-letters");
    if (!buckets) return;
    new Chart(canvas[0], {
      type: "bar",
      data: {
        labels: buckets.map((bucket) => bucket.letter),
        datasets: [{
          label: "Artists",
          data: buckets.map((bucket) => bucket.count),
          backgroundColor: accentColor,
          borderColor: accentBorderColor,
          borderWidth: 1,
          borderRadius: 3
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { display: false },
          tooltip: { callbacks: { title: (items) => items[0]?.label ?? "" } }
        },
        scales: {
          x: { grid: { display: false }, ticks: { color: axisTickColor } },
          y: { beginAtZero: true, grid: { color: chartGridColor }, ticks: { color: axisTickColor, precision: 0 } }
        }
      }
    });
  }
  function renderHorizontalBarChart(canvasId, dataAttribute, barColor, labelKey, valueKey) {
    const canvas = $("#" + canvasId);
    if (!canvas.length) return;
    const items = readJsonAttribute(canvas, dataAttribute);
    if (!items || items.length === 0) return;
    new Chart(canvas[0], {
      type: "bar",
      data: {
        labels: items.map((item) => item[labelKey]),
        datasets: [{
          label: "Releases",
          data: items.map((item) => item[valueKey]),
          backgroundColor: barColor,
          borderColor: barColor,
          borderWidth: 1,
          borderRadius: 3
        }]
      },
      options: {
        indexAxis: "y",
        responsive: true,
        maintainAspectRatio: false,
        plugins: { legend: { display: false } },
        scales: {
          x: { beginAtZero: true, grid: { color: chartGridColor }, ticks: { color: axisTickColor, precision: 0 } },
          y: { grid: { display: false }, ticks: { color: axisLabelColor, autoSkip: false } }
        }
      }
    });
  }
  function renderTopCountChart(canvasId, dataAttribute, barColor) {
    renderHorizontalBarChart(canvasId, dataAttribute, barColor, "name", "count");
  }
  function renderTopArtistsChart(canvasId) {
    const canvas = $("#" + canvasId);
    if (!canvas.length) return;
    const artists = readJsonAttribute(canvas, "data-stats-artists");
    if (!artists || artists.length === 0) return;
    new Chart(canvas[0], {
      type: "bar",
      data: {
        labels: artists.map((artist) => artist.name),
        datasets: [{
          label: "Releases",
          data: artists.map((artist) => artist.releaseCount),
          backgroundColor: artistsBarColor,
          borderColor: artistsBarColor,
          borderWidth: 1,
          borderRadius: 3
        }]
      },
      options: {
        indexAxis: "y",
        responsive: true,
        maintainAspectRatio: false,
        plugins: { legend: { display: false } },
        scales: {
          x: { beginAtZero: true, grid: { color: chartGridColor }, ticks: { color: axisTickColor, precision: 0 } },
          y: { grid: { display: false }, ticks: { color: axisLabelColor, autoSkip: false } }
        }
      }
    });
  }
  function initStatsCharts() {
    applyChartDefaults();
    renderLetterDistributionChart("lettersChart");
    renderTopCountChart("stylesChart", "data-stats-bars", stylesBarColor);
    renderTopCountChart("genresChart", "data-stats-bars", genresBarColor);
    renderTopArtistsChart("artistsChart");
  }
  $(initStatsCharts);
})();
