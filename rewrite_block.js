const fs = require("fs");
let content = fs.readFileSync("index.html", "utf8");
const startMarker = "        // AutoCAD beam label defaults\r\n        try {";
const endMarker = "        document.getElementById(\"configBtn\").disabled = false;\r\n";
const startIndex = content.indexOf(startMarker);
const endIndex = content.indexOf(endMarker, startIndex);
if (startIndex === -1 || endIndex === -1) {
  throw new Error('Block not found');
}
const replacement = `        // Restore saved manual grid configuration, if present.\r\n        try {\r\n          const saved = localStorage.getItem('userGridConfig');\r\n          if (saved) {\r\n            userGridConfig = JSON.parse(saved);\r\n            console.log('Loaded saved manual grid configuration.');\r\n          }\r\n        } catch (e) {\r\n          console.error('Failed to load manual grid configuration:', e);\r\n        }\r\n\r\n        // Collect every beam across all stories\r\n        let allBeamsAcrossStories = [];\r\n        for (const story of availableStories) {\r\n          const frames = parseFrames(previewFileContent, story);\r\n          if (frames.length > 0) allBeamsAcrossStories.push(...frames);\r\n        }\r\n\r\n        fullDrawableBeams = allBeamsAcrossStories.map((f) => ({\r\n          ...f,\r\n          j1: previewJoints[f.joint1],\r\n          j2: previewJoints[f.joint2],\r\n        }));\r\n\r\n        storyOrderInfo = availableStories.reduce((acc, story, index) => {\r\n          acc[story] = index;\r\n          return acc;\r\n        }, {});\r\n\r\n        // Default selection\r\n        if (availableStories.includes("2F")) {\r\n          storySelector.value = "2F";\r\n        }\r\n        handleStoryChange();\r\n\r\n        statusDiv.innerHTML = '<p>Processed ' + availableStories.length + ' stories and ' + allBeamsAcrossStories.length + ' beams.</p>';\r\n\r\n        // Enable manual grid configuration\r\n`;
const newContent = content.slice(0, startIndex) + replacement + content.slice(endIndex);
fs.writeFileSync("index.html", newContent);
