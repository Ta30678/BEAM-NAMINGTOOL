const fs = require("fs");
let content = fs.readFileSync("index.html", "utf8");
const mappings = new Map([
  ["console.log('? ?w???J??u?s???W?h');", "console.log('Loaded saved manual grid configuration.');"],
  ["console.error('???J??u?s???W?h????:', e);", "console.error('Failed to load manual grid configuration:', e);"],
  ["statusDiv.innerHTML = `<p>? ?w???????I??? ${availableStories.length} ???h?A${allBeamsAcrossStories.length} ???</p>`;", "statusDiv.innerHTML = `<p>Processed ${availableStories.length} stories and ${allBeamsAcrossStories.length} beams.</p>`;"],
  ["statusDiv.innerHTML = `<p class=\"error\">?w??????: ${error.message}</p>`;", "statusDiv.innerHTML = `<p class=\"error\">Processing failed: ${error.message}</p>`;"],
  ["const storySectionMatch = previewFileContent.match(\n            /\\$ STORIES - IN SEQUENCE FROM TOP([\\s\\S]*?)(?=\\$|$)/i,\n          );\n          if (!storySectionMatch) throw new Error(\"?????h?w?q\");", "const storySectionMatch = previewFileContent.match(\n            /\\$ STORIES - IN SEQUENCE FROM TOP([\\s\\S]*?)(?=\\$|$)/i,\n          );\n          if (!storySectionMatch) throw new Error(\"Unable to locate story definitions.\");"],
  ["if (allStoryNames.length === 0) throw new Error(\"??h?w?q????\");", "if (allStoryNames.length === 0) throw new Error(\"Story definitions are empty.\");"],
  ["// ??R??h", "// Parse story data"],
  ["// ??R??u?M?`?I", "// Parse grids and joints"],
  ["// ??R?????????", "// Collect all beams across the stories"],
  ["// ???w????", "// Default preview selection"],
  ["// ??£Tu?]?w?s???W?h?e???v???s", "// Enable manual grid configuration"],
]);

mappings.forEach((replacement, original) => {
  if (!content.includes(original)) {
    console.warn(`Missing substring: ${original}`);
    return;
  }
  content = content.replace(original, replacement);
});

fs.writeFileSync("index.html", content);
