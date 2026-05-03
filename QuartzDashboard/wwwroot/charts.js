/**
 * ChartEngine — Pure SVG chart rendering for QuartzDashboard.
 * Zero dependencies, inline SVG, cubic spline interpolation.
 */
window.ChartEngine = (function() {
  'use strict';

  // ===== Interpolation =====

  /**
   * Monotone cubic spline interpolation through data points.
   * Produces smooth curves that don't overshoot (no false peaks).
   * Returns SVG path "d" attribute string.
   */
  function smoothPath(points, xField, yField, xScale, yScale) {
    if (!points || points.length < 2) return '';

    var n = points.length;
    var xs = new Array(n);
    var ys = new Array(n);
    for (var i = 0; i < n; i++) {
      xs[i] = xScale(points[i][xField] !== undefined ? points[i][xField] : i);
      ys[i] = yScale(points[i][yField] || 0);
    }

    // Compute slopes
    var dx = new Array(n - 1);
    var dy = new Array(n - 1);
    var slopes = new Array(n - 1);
    for (i = 0; i < n - 1; i++) {
      dx[i] = xs[i + 1] - xs[i];
      dy[i] = ys[i + 1] - ys[i];
      slopes[i] = dx[i] !== 0 ? dy[i] / dx[i] : 0;
    }

    // Compute tangent slopes (Fritsch-Carlson monotone)
    var tangents = new Array(n);
    tangents[0] = slopes[0];
    tangents[n - 1] = slopes[n - 2];
    for (i = 1; i < n - 1; i++) {
      if (slopes[i - 1] * slopes[i] <= 0) {
        tangents[i] = 0;
      } else {
        tangents[i] = (slopes[i - 1] + slopes[i]) / 2;
      }
    }

    // Build SVG path using cubic bezier curves
    var d = 'M' + xs[0].toFixed(1) + ',' + ys[0].toFixed(1);
    for (i = 0; i < n - 1; i++) {
      var cp1x = xs[i] + dx[i] / 3;
      var cp1y = ys[i] + tangents[i] * dx[i] / 3;
      var cp2x = xs[i + 1] - dx[i] / 3;
      var cp2y = ys[i + 1] - tangents[i + 1] * dx[i] / 3;
      d += 'C' + cp1x.toFixed(1) + ',' + cp1y.toFixed(1) + ' ' +
           cp2x.toFixed(1) + ',' + cp2y.toFixed(1) + ' ' +
           xs[i + 1].toFixed(1) + ',' + ys[i + 1].toFixed(1);
    }
    return d;
  }

  /**
   * Area path: smooth curve + close to bottom edge for fill.
   */
  function areaPath(points, xField, yField, xScale, yScale, bottomY) {
    var top = smoothPath(points, xField, yField, xScale, yScale);
    if (!top || points.length < 2) return '';
    var last = points.length - 1;
    var xLast = xScale(points[last][xField] !== undefined ? points[last][xField] : last);
    var xFirst = xScale(points[0][xField] !== undefined ? points[0][xField] : 0);
    return top + 'L' + xLast.toFixed(1) + ',' + bottomY + 'L' + xFirst.toFixed(1) + ',' + bottomY + 'Z';
  }

  // ===== Axis =====

  /**
   * Generate nice round Y-axis ticks.
   * Returns array of {value, label, y}
   */
  function yAxisTicks(maxVal, height, margin, count) {
    count = count || 5;
    if (!maxVal || maxVal <= 0) maxVal = 1;

    // Find a nice interval
    var rough = maxVal / (count - 1);
    var mag = Math.pow(10, Math.floor(Math.log10(rough)));
    var norm = rough / mag;
    var nice;
    if (norm < 1.5) nice = 1;
    else if (norm < 3.5) nice = 2;
    else if (norm < 7.5) nice = 5;
    else nice = 10;
    var interval = nice * mag;

    var ticks = [];
    var h = height - margin.top - margin.bottom;
    for (var v = 0; v <= maxVal + interval; v += interval) {
      if (ticks.length >= count) break;
      var y = margin.top + h - (v / (maxVal || 1)) * h;
      ticks.push({
        value: v,
        label: formatTick(v),
        y: y
      });
    }
    return ticks;
  }

  function formatTick(v) {
    if (v >= 1000000) return (v / 1000000).toFixed(1) + 'M';
    if (v >= 1000) return (v / 1000).toFixed(1) + 'k';
    if (v === Math.floor(v)) return v.toString();
    return v.toFixed(1);
  }

  /**
   * Generate X-axis time labels.
   */
  function xAxisTimeLabels(data, timeField, width, margin, maxLabels) {
    maxLabels = maxLabels || 8;
    if (!data || data.length < 2) return [];
    var w = width - margin.left - margin.right;
    var step = Math.max(1, Math.floor(data.length / maxLabels));
    var labels = [];
    for (var i = 0; i < data.length; i += step) {
      var x = margin.left + (i / Math.max(data.length - 1, 1)) * w;
      var timeStr = data[i][timeField] || '';
      labels.push({ x: x, label: timeStr, index: i });
    }
    // Always include last label
    var last = data.length - 1;
    var lastX = margin.left + (last / Math.max(data.length - 1, 1)) * w;
    if (labels.length === 0 || labels[labels.length - 1].x < lastX - 20) {
      labels.push({ x: lastX, label: data[last][timeField] || '', index: last });
    }
    return labels;
  }

  // ===== Scales =====

  function scaleLinear(rangeMin, rangeMax, domainMin, domainMax) {
    var r = rangeMax - rangeMin;
    var d = domainMax - domainMin || 1;
    return function(val) {
      return rangeMin + ((val - domainMin) / d) * r;
    };
  }

  function scalePoint(rangeMin, rangeMax, count) {
    var step = count > 1 ? (rangeMax - rangeMin) / (count - 1) : 0;
    return function(idx) {
      return rangeMin + idx * step;
    };
  }

  // ===== Gradients =====

  /**
   * Returns SVG <defs> string with gradient definitions.
   */
  function gradientDefs(prefix) {
    return [
      '<defs>',
      '  <linearGradient id="' + prefix + 'countGrad" x1="0" y1="0" x2="0" y2="1">',
      '    <stop offset="0%" stop-color="#818cf8" stop-opacity="0.15"/>',
      '    <stop offset="100%" stop-color="#818cf8" stop-opacity="0"/>',
      '  </linearGradient>',
      '  <linearGradient id="' + prefix + 'durationGrad" x1="0" y1="0" x2="0" y2="1">',
      '    <stop offset="0%" stop-color="#34d399" stop-opacity="0.15"/>',
      '    <stop offset="100%" stop-color="#34d399" stop-opacity="0"/>',
      '  </linearGradient>',
      '  <linearGradient id="' + prefix + 'errorGrad" x1="0" y1="0" x2="0" y2="1">',
      '    <stop offset="0%" stop-color="#ef4444" stop-opacity="0.15"/>',
      '    <stop offset="100%" stop-color="#ef4444" stop-opacity="0"/>',
      '  </linearGradient>',
      '  <filter id="' + prefix + 'glow" x="-20%" y="-20%" width="140%" height="140%">',
      '    <feGaussianBlur stdDeviation="2" result="blur"/>',
      '    <feMerge><feMergeNode in="blur"/><feMergeNode in="SourceGraphic"/></feMerge>',
      '  </filter>',
      '</defs>'
    ].join('\n');
  }

  // ===== Grid =====

  function gridLines(ticks, width, margin) {
    var lines = '';
    var w = width - margin.left - margin.right;
    for (var i = 0; i < ticks.length; i++) {
      lines += '<line x1="' + margin.left + '" y1="' + ticks[i].y +
               '" x2="' + (margin.left + w) + '" y2="' + ticks[i].y +
               '" stroke="rgba(255,255,255,0.04)" stroke-width="0.5" stroke-dasharray="3,3"/>';
    }
    return lines;
  }

  // ===== Bar Chart =====

  function barRects(data, field, width, height, margin, color) {
    if (!data || !data.length) return [];
    var w = width - margin.left - margin.right;
    var h = height - margin.top - margin.bottom;
    var maxVal = 0;
    for (var i = 0; i < data.length; i++) {
      if (data[i][field] > maxVal) maxVal = data[i][field];
    }
    if (maxVal <= 0) maxVal = 1;
    var barW = Math.max(2, (w / data.length) - 2);
    var rects = [];
    for (i = 0; i < data.length; i++) {
      var val = data[i][field] || 0;
      var barH = (val / maxVal) * h;
      var x = margin.left + (i / data.length) * w + 1;
      var y = margin.top + h - barH;
      rects.push({ x: x, y: y, width: barW, height: barH, value: val, index: i, data: data[i] });
    }
    return rects;
  }

  // ===== Heatmap =====

  function heatmapCells(data, field, width, height, margin, rows, cols) {
    if (!data || !data.length) return [];
    var w = width - margin.left - margin.right;
    var h = height - margin.top - margin.bottom;
    rows = rows || 8;
    cols = cols || Math.min(data.length, 60);
    var maxVal = 0;
    for (var i = 0; i < data.length; i++) {
      if (data[i][field] > maxVal) maxVal = data[i][field];
    }
    if (maxVal <= 0) maxVal = 1;
    var cellW = w / cols;
    var cellH = h / rows;
    var cells = [];
    for (var r = 0; r < rows; r++) {
      for (var c = 0; c < cols && c < data.length; c++) {
        var idx = r * cols + c;
        if (idx >= data.length) break;
        var val = data[idx][field] || 0;
        var intensity = val / maxVal;
        // Color: white (0) -> indigo (0.5) -> red (1)
        var rv = Math.round(99 + intensity * 140);  // 99 -> 239
        var gv = Math.round(102 - intensity * 60);  // 102 -> 42
        var bv = Math.round(241 - intensity * 200); // 241 -> 41
        cells.push({
          x: margin.left + c * cellW,
          y: margin.top + r * cellH,
          width: cellW,
          height: cellH,
          fill: 'rgb(' + Math.min(239, rv) + ',' + Math.max(42, gv) + ',' + Math.max(41, bv) + ')',
          value: val,
          data: data[idx]
        });
      }
    }
    return cells;
  }

  // ===== Sparkline =====

  function sparklinePolyline(data, field, w, h, color) {
    if (!data || data.length < 2) return '';
    var values = [];
    for (var i = 0; i < data.length; i++) {
      values.push(data[i][field] || 0);
    }
    var maxVal = 0;
    for (i = 0; i < values.length; i++) {
      if (values[i] > maxVal) maxVal = values[i];
    }
    if (maxVal <= 0) maxVal = 1;
    var stepX = w / Math.max(values.length - 1, 1);
    var pts = [];
    for (i = 0; i < values.length; i++) {
      var x = i * stepX;
      var y = h - (values[i] / maxVal) * (h - 2) - 1;
      pts.push(x.toFixed(1) + ',' + y.toFixed(1));
    }
    return pts.join(' ');
  }

  // ===== PNG Export =====

  function exportPNG(svgElement, filename) {
    filename = filename || 'chart.png';
    var serializer = new XMLSerializer();
    var svgStr = serializer.serializeToString(svgElement);
    var canvas = document.createElement('canvas');
    var ctx = canvas.getContext('2d');
    var img = new Image();
    var blob = new Blob([svgStr], { type: 'image/svg+xml;charset=utf-8' });
    var url = URL.createObjectURL(blob);
    img.onload = function() {
      canvas.width = img.width * 2;
      canvas.height = img.height * 2;
      ctx.scale(2, 2);
      ctx.fillStyle = '#030712';
      ctx.fillRect(0, 0, canvas.width, canvas.height);
      ctx.drawImage(img, 0, 0);
      URL.revokeObjectURL(url);
      var a = document.createElement('a');
      a.download = filename;
      a.href = canvas.toDataURL('image/png');
      a.click();
    };
    img.src = url;
  }

  // ===== Public API =====

  return {
    smoothPath: smoothPath,
    areaPath: areaPath,
    yAxisTicks: yAxisTicks,
    xAxisTimeLabels: xAxisTimeLabels,
    scaleLinear: scaleLinear,
    scalePoint: scalePoint,
    gradientDefs: gradientDefs,
    gridLines: gridLines,
    barRects: barRects,
    heatmapCells: heatmapCells,
    sparklinePolyline: sparklinePolyline,
    exportPNG: exportPNG,
  };
})();
