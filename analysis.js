const fs = require('fs');

const TOLERANCE = 0.1;
const SYMMETRY_TOLERANCE = 0.5;
const MATCHING_TOLERANCE = 0.8;
const DIRECTION_TOLERANCE = 0.01;
const GEOMETRIC_TOLERANCE = 0.01;

function parseGrids(content) {
  const grids = { x: [], y: [] };
  const gridTableSection = content.match(/TABLE:\s+"GRID DEFINITIONS - LINES"([\s\S]*?)(?=TABLE:|$)/);
  if (gridTableSection) {
    let currentGrid = {};
    const lines = gridTableSection[1].trim().split('\n');
    lines.forEach((line) => {
      const trimmed = line.trim();
      if (trimmed === '') {
        if (currentGrid.name && currentGrid.type && currentGrid.ordinate !== undefined) {
          if (currentGrid.type.toUpperCase() === 'X') grids.x.push(currentGrid);
          else if (currentGrid.type.toUpperCase() === 'Y') grids.y.push(currentGrid);
        }
        currentGrid = {};
      } else {
        const match = trimmed.match(/(\S+)\s*=\s*"?([^"]*)"?/);
        if (match) {
          const [, key, value] = match;
          if (key === 'GridID') currentGrid.name = value;
          if (key === 'GridType') currentGrid.type = value;
          if (key === 'Ordinate') currentGrid.ordinate = parseFloat(value);
          if (key === 'BubbleLoc') currentGrid.bubbleLoc = value;
          if (key === 'LineType') currentGrid.lineType = value;
        }
      }
    });
    if (currentGrid.name) {
      if (currentGrid.type.toUpperCase() === 'X') grids.x.push(currentGrid);
      else if (currentGrid.type.toUpperCase() === 'Y') grids.y.push(currentGrid);
    }
  }
  if (grids.x.length === 0 && grids.y.length === 0) {
    const gridDollarSection = content.match(/\$ GRIDS([\s\S]*?)(?=\$|$)/);
    if (gridDollarSection) {
      const lines = gridDollarSection[1].trim().split('\n');
      lines.forEach((line) => {
        const match = line.match(/LABEL\s+"([^"]+)"\s+DIR\s+"(X|Y)"\s+COORD\s+([-\d\.E]+)/i);
        if (match) {
          const [, name, type, ordinate] = match;
          const info = { name, type, ordinate: parseFloat(ordinate) };
          if (type.toUpperCase() === 'X') grids.x.push(info);
          else if (type.toUpperCase() === 'Y') grids.y.push(info);
        }
      });
    }
  }
  if (grids.x.length === 0 && grids.y.length === 0) {
    throw new Error('Could not find grid definitions.');
  }
  grids.x = [...new Map(grids.x.map((item) => [item.name, item])).values()].sort((a, b) => a.ordinate - b.ordinate);
  grids.y = [...new Map(grids.y.map((item) => [item.name, item])).values()].sort((a, b) => a.ordinate - b.ordinate);
  return grids;
}

function parseJoints(content) {
  const joints = {};
  const sciNotationRegex = /"([^"]+)"\s+([-\d\.E]+)\s+([-\d\.E]+)/;
  const pointRegex = /^POINT\s+"([^"]+)"\s+([-\d\.E]+)\s+([-\d\.E]+)/;
  const nameRegex = /NAME\s*=\s*(\S+)\s*X\s*=\s*([-\d\.E]+)\s*Y\s*=\s*([-\d\.E]+)/;

  const jointTableSection = content.match(/TABLE:\s+"JOINT COORDINATES"([\s\S]*?)(?=TABLE:|$)/);
  if (jointTableSection) {
    const lines = jointTableSection[1].trim().split('\n');
    lines.forEach((line) => {
      if (line.trim().startsWith('JOINT')) return;
      const match = line.trim().match(sciNotationRegex);
      if (match) {
        joints[match[1]] = { x: parseFloat(match[2]), y: parseFloat(match[3]) };
      }
    });
  }
  if (Object.keys(joints).length === 0) {
    const lines = content.split('\n');
    lines.forEach((line) => {
      const match = line.trim().match(pointRegex);
      if (match) {
        joints[match[1]] = { x: parseFloat(match[2]), y: parseFloat(match[3]) };
      }
    });
  }
  if (Object.keys(joints).length === 0) {
    const jointDollarSection = content.match(/\$ JOINTS([\s\S]*?)(?=\$|$)/s);
    if (jointDollarSection) {
      const lines = jointDollarSection[1].trim().split('\n');
      lines.forEach((line) => {
        const match = line.match(nameRegex);
        if (match) {
          joints[match[1]] = { x: parseFloat(match[2]), y: parseFloat(match[3]) };
        }
      });
    }
  }
  if (Object.keys(joints).length === 0) {
    throw new Error('Could not find joint coordinate definitions.');
  }
  return joints;
}

function parseFrames(content, story) {
  const frames = [];
  const isBeamProp = (propName) => {
    if (!propName) return false;
    const p = propName.toUpperCase();
    const isNumericBeam = /^\d+(\.\d+)?B/.test(p);
    return (
      isNumericBeam ||
      p.startsWith('B') ||
      p.startsWith('G') ||
      p.startsWith('SB') ||
      p.startsWith('WB') ||
      p.startsWith('FB') ||
      p.startsWith('FGB') ||
      p.startsWith('FSB') ||
      p.startsWith('FWB')
    );
  };

  const frameTableSection = content.match(/TABLE:\s+"CONNECTIVITY - FRAME"([\s\S]*?)(?=TABLE:|$)/);
  const frameAssignSection = content.match(/TABLE:\s+"FRAME ASSIGNS - SECTION"([\s\S]*?)(?=TABLE:|$)/);
  if (frameTableSection && frameAssignSection) {
    const connectivityLines = frameTableSection[1].trim().split('\n');
    const assignLines = frameAssignSection[1].trim().split('\n');
    const frameProperties = new Map();
    assignLines.forEach((line) => {
      const match = line.trim().match(/^"([^"]+)"\s+"([^"]+)"\s+"([^"]+)"/);
      if (match && match[2] === story) {
        frameProperties.set(match[1], match[3]);
      }
    });
    connectivityLines.forEach((line) => {
      const match = line.trim().match(/^"([^"]+)"\s+"([^"]+)"\s+"([^"]+)"/);
      if (match) {
        const [, name, joint1, joint2] = match;
        const propName = frameProperties.get(name);
        if (isBeamProp(propName)) {
          frames.push({ name, prop: propName, joint1, joint2, story });
        }
      }
    });
  }
  if (frames.length === 0) {
    const lineConnectivitySection = content.match(/\$ LINE CONNECTIVITIES([\s\S]*?)(?=\$|$)/i);
    const lineAssignsSection = content.match(/\$ LINE ASSIGNS([\s\S]*?)(?=\$|$)/i);
    if (lineConnectivitySection && lineAssignsSection) {
      const connectLines = lineConnectivitySection[1].trim().split('\n');
      const assignLines = lineAssignsSection[1].trim().split('\n');
      const beamAssigns = new Map();
      assignLines.forEach((line) => {
        const assignMatch = line.match(new RegExp(`LINEASSIGN\\s+"([^\"]+)"\\s+"${story}"\\s+SECTION\\s+"([^\"]+)"`, 'i'));
        if (assignMatch) {
          const [, name, propName] = assignMatch;
          if (isBeamProp(propName)) {
            beamAssigns.set(name, propName);
          }
        }
      });
      connectLines.forEach((line) => {
        const connMatch = line.match(/LINE\s+"([^"]+)"\s+BEAM\s+"([^"]+)"\s+"([^"]+)"/i);
        if (connMatch) {
          const [, name, joint1, joint2] = connMatch;
          if (beamAssigns.has(name)) {
            frames.push({ name, prop: beamAssigns.get(name), joint1, joint2, story });
          }
        }
      });
    }
  }
  return frames;
}

function distance(p1, p2) {
  if (!p1 || !p2) return Infinity;
  return Math.sqrt(Math.pow(p1.x - p2.x, 2) + Math.pow(p1.y - p2.y, 2));
}

function isPointOnSegment(point, segP1, segP2, tolerance) {
  if (!point || !segP1 || !segP2) return false;
  const segLength = distance(segP1, segP2);
  if (segLength < tolerance) {
    return distance(point, segP1) < tolerance;
  }
  const distSum = distance(point, segP1) + distance(point, segP2);
  return Math.abs(distSum - segLength) < tolerance;
}

function detectSymmetryAxis(beams, joints, gridData) {
  if (!beams || beams.length < 10) return null;
  const xCoords = [];
  beams.forEach((beam) => {
    const j1 = joints[beam.joint1];
    const j2 = joints[beam.joint2];
    if (j1 && j2) {
      xCoords.push(j1.x, j2.x);
    }
  });
  if (xCoords.length === 0) return null;

  const minX = Math.min(...xCoords);
  const maxX = Math.max(...xCoords);
  const centerX = (minX + maxX) / 2;

  const candidates = [centerX];
  if (gridData && gridData.x) {
    gridData.x.forEach((grid) => {
      if (grid.ordinate > minX && grid.ordinate < maxX) {
        candidates.push(grid.ordinate);
      }
    });
  }

  let bestAxis = null;
  let bestScore = 0;

  candidates.forEach((axisX) => {
    let matchCount = 0;
    let totalCount = 0;

    beams.forEach((beam) => {
      const j1 = joints[beam.joint1];
      const j2 = joints[beam.joint2];
      if (!j1 || !j2) return;

      const midX = (j1.x + j2.x) / 2;
      const midY = (j1.y + j2.y) / 2;
      const length = distance(j1, j2);

      if (Math.abs(midX - axisX) < SYMMETRY_TOLERANCE) return;

      totalCount++;

      const mirroredX = 2 * axisX - midX;
      const candidateBeam = beams.find((other) => {
        if (other === beam) return false;
        const otherJ1 = joints[other.joint1];
        const otherJ2 = joints[other.joint2];
        if (!otherJ1 || !otherJ2) return false;
        const otherMidX = (otherJ1.x + otherJ2.x) / 2;
        const otherMidY = (otherJ1.y + otherJ2.y) / 2;
        const otherLength = distance(otherJ1, otherJ2);
        return (
          Math.abs(otherMidX - mirroredX) < MATCHING_TOLERANCE &&
          Math.abs(otherMidY - midY) < MATCHING_TOLERANCE &&
          Math.abs(otherLength - length) < MATCHING_TOLERANCE
        );
      });

      if (candidateBeam) {
        matchCount++;
      }
    });

    const score = totalCount > 0 ? matchCount / totalCount : 0;
    if (score > bestScore) {
      bestScore = score;
      bestAxis = axisX;
    }
  });

  if (bestScore > 0.7) {
    return bestAxis;
  }
  return null;
}

function getComponentBounds(component, joints) {
  if (!component || component.length === 0) return null;
  let minX = Infinity, maxX = -Infinity, minY = Infinity, maxY = -Infinity;
  const uniquePoints = new Set();
  component.forEach((beam) => {
    const p1 = joints[beam.joint1];
    const p2 = joints[beam.joint2];
    if (p1) uniquePoints.add(`${p1.x},${p1.y}`);
    if (p2) uniquePoints.add(`${p2.x},${p2.y}`);
  });
  if (uniquePoints.size === 0) return null;
  uniquePoints.forEach((key) => {
    const [x, y] = key.split(',').map(Number);
    minX = Math.min(minX, x);
    maxX = Math.max(maxX, x);
    minY = Math.min(minY, y);
    maxY = Math.max(maxY, y);
  });
  return { minX, maxX, minY, maxY };
}

function mirrorPoint(point, axisX) {
  return { x: 2 * axisX - point.x, y: point.y };
}

function isBeamOnSymmetryAxis(beam, joints, axisX, tolerance) {
  const p1 = joints[beam.joint1];
  const p2 = joints[beam.joint2];
  if (!p1 || !p2) return false;
  const midX = (p1.x + p2.x) / 2;
  if (Math.abs(midX - axisX) < tolerance) {
    return true;
  }
  const p1DistFromAxis = p1.x - axisX;
  const p2DistFromAxis = p2.x - axisX;
  if (
    Math.abs(p1DistFromAxis) > tolerance &&
    Math.abs(p2DistFromAxis) > tolerance &&
    p1DistFromAxis * p2DistFromAxis < 0
  ) {
    return true;
  }
  if (
    Math.abs(p1DistFromAxis) < tolerance ||
    Math.abs(p2DistFromAxis) < tolerance
  ) {
    return true;
  }
  return false;
}

function findBuildingComponents(allBeamsOnStory, joints, useMirrorMode = false, globalSymmetryAxisX = null) {
  if (!allBeamsOnStory || allBeamsOnStory.length === 0) return [];

  const components = [];
  const processedBeams = new Set();

  const beamsWithCoords = allBeamsOnStory
    .map((b) => ({ ...b, p1: joints[b.joint1], p2: joints[b.joint2] }))
    .filter((b) => b.p1 && b.p2);

  function areBeamsConnected(beamA, beamB) {
    if (distance(beamA.p1, beamB.p1) < GEOMETRIC_TOLERANCE) return true;
    if (distance(beamA.p1, beamB.p2) < GEOMETRIC_TOLERANCE) return true;
    if (distance(beamA.p2, beamB.p1) < GEOMETRIC_TOLERANCE) return true;
    if (distance(beamA.p2, beamB.p2) < GEOMETRIC_TOLERANCE) return true;
    if (isPointOnSegment(beamA.p1, beamB.p1, beamB.p2, GEOMETRIC_TOLERANCE)) return true;
    if (isPointOnSegment(beamA.p2, beamB.p1, beamB.p2, GEOMETRIC_TOLERANCE)) return true;
    if (isPointOnSegment(beamB.p1, beamA.p1, beamA.p2, GEOMETRIC_TOLERANCE)) return true;
    if (isPointOnSegment(beamB.p2, beamA.p1, beamA.p2, GEOMETRIC_TOLERANCE)) return true;
    return false;
  }

  for (const startBeam of beamsWithCoords) {
    if (processedBeams.has(startBeam.name)) {
      continue;
    }

    const currentComponent = [];
    const queue = [startBeam];
    processedBeams.add(startBeam.name);

    while (queue.length > 0) {
      const currentBeam = queue.shift();
      currentComponent.push(currentBeam);

      for (const otherBeam of beamsWithCoords) {
        if (processedBeams.has(otherBeam.name)) continue;
        if (areBeamsConnected(currentBeam, otherBeam)) {
          processedBeams.add(otherBeam.name);
          queue.push(otherBeam);
        }
      }
    }

    if (currentComponent.length > 0) {
      components.push(currentComponent);
    }
  }

  if (useMirrorMode && components.length === 1 && beamsWithCoords.length > 0) {
    const singleComponent = components[0];
    let symmetryAxisX = globalSymmetryAxisX;
    if (symmetryAxisX === null) {
      const allXCoords = [];
      singleComponent.forEach((b) => {
        allXCoords.push(b.p1.x, b.p2.x);
      });
      const minX = Math.min(...allXCoords);
      const maxX = Math.max(...allXCoords);
      symmetryAxisX = (minX + maxX) / 2;
    }

    const leftBeams = [];
    const centerBeams = [];
    const rightBeams = [];

    singleComponent.forEach((beam) => {
      const midX = (beam.p1.x + beam.p2.x) / 2;
      const distFromAxis = Math.abs(midX - symmetryAxisX);
      if (distFromAxis < SYMMETRY_TOLERANCE) {
        centerBeams.push(beam);
      } else if (midX < symmetryAxisX) {
        leftBeams.push(beam);
      } else {
        rightBeams.push(beam);
      }
    });

    const newComponents = [];
    if (leftBeams.length > 0 || centerBeams.length > 0) {
      newComponents.push([...leftBeams, ...centerBeams]);
    }
    if (rightBeams.length > 0) {
      newComponents.push(rightBeams);
    }
    return newComponents;
  }

  return components;
}

const file = '2023-0725.e2k';
const content = fs.readFileSync(file, 'utf8');

const storySectionMatch = content.match(/\$ STORIES - IN SEQUENCE FROM TOP([\s\S]*?)(?=\$|$)/i);
if (!storySectionMatch) {
  throw new Error('No story definitions found');
}
const stories = storySectionMatch[1]
  .trim()
  .split('\n')
  .map((line) => {
    const m = line.match(/STORY\s+"([^\"]+)"/i);
    return m ? m[1] : null;
  })
  .filter(Boolean);

const joints = parseJoints(content);
const gridData = parseGrids(content);

const allBeamsAcrossStories = [];
stories.forEach((story) => {
  const frames = parseFrames(content, story);
  if (frames.length > 0) {
    allBeamsAcrossStories.push(...frames);
  }
});

const mainBeams = allBeamsAcrossStories.filter((b) => {
  const prop = b.prop.toUpperCase();
  const isNumericBeam = /^\d+(\.\d+)?B/.test(prop);
  return (
    (isNumericBeam ||
      prop.startsWith('B') ||
      prop.startsWith('G') ||
      prop.startsWith('FB') ||
      prop.startsWith('FGB')) &&
    !prop.startsWith('SB') &&
    !prop.startsWith('FSB')
  );
});
const secondaryBeams = allBeamsAcrossStories.filter(
  (b) => b.prop.toUpperCase().startsWith('SB') && !b.prop.toUpperCase().startsWith('FSB'),
);

console.log('Stories:', stories.join(', '));
console.log('Total beams -> main:', mainBeams.length, 'secondary:', secondaryBeams.length);

let globalSymmetryAxisX = null;
const axisCandidates = [];
stories.forEach((story) => {
  const secondary = secondaryBeams.filter((b) => b.story === story);
  const main = mainBeams.filter((b) => b.story === story);
  const combined = [...secondary, ...main];
  if (combined.length < 10) return;
  const axis = detectSymmetryAxis(combined, joints, gridData);
  if (axis !== null) {
    axisCandidates.push({ story, axis });
  }
});
if (axisCandidates.length > 0) {
  const AXIS_TOLERANCE = 0.5;
  const groups = [];
  axisCandidates.forEach((candidate) => {
    let found = false;
    for (const group of groups) {
      if (Math.abs(group[0].axis - candidate.axis) < AXIS_TOLERANCE) {
        group.push(candidate);
        found = true;
        break;
      }
    }
    if (!found) {
      groups.push([candidate]);
    }
  });
  groups.sort((a, b) => b.length - a.length);
  const bestGroup = groups[0];
  globalSymmetryAxisX = bestGroup.reduce((sum, c) => sum + c.axis, 0) / bestGroup.length;
  console.log('Global symmetry axis ≈', globalSymmetryAxisX.toFixed(3), 'based on stories', bestGroup.map((c) => c.story).join(', '));
} else {
  console.log('No global symmetry axis detected.');
}

function analyzeStory(story) {
  console.log(`\n===== Analysis for ${story} =====`);
  const secondary = secondaryBeams.filter((b) => b.story === story);
  const main = mainBeams.filter((b) => b.story === story);
  console.log(`secondary beams: ${secondary.length}, main beams: ${main.length}`);
  const allBeamsOnStory = [...secondary, ...main];
  const rawComponents = findBuildingComponents(allBeamsOnStory, joints, true, globalSymmetryAxisX);
  console.log('raw components:', rawComponents.length);
  rawComponents.forEach((comp, idx) => {
    const secondaryCount = comp.filter((beam) => secondary.some((sb) => sb.name === beam.name)).length;
    const namesPreview = comp.slice(0, 5).map((beam) => beam.name).join(', ');
    console.log(
      `  raw[${idx}] -> total beams: ${comp.length}, secondary count: ${secondaryCount}, sample: ${namesPreview}`,
    );
  });
  const components = rawComponents
    .map((comp) =>
      comp.filter((beam) => secondary.some((sb) => sb.name === beam.name)),
    )
    .filter((comp) => comp.length > 0)
    .map((comp) => ({ component: comp, bounds: getComponentBounds(comp, joints) }))
    .sort((a, b) => a.bounds.minX - b.bounds.minX);

  console.log('components containing secondary beams:', components.length);
  components.forEach((comp, idx) => {
    const { minX, maxX, minY, maxY } = comp.bounds;
    const namesPreview = comp.component.slice(0, 5).map((beam) => beam.name).join(', ');
    console.log(
      `  component[${idx}] -> secondary count: ${comp.component.length}, bounds X:[${minX.toFixed(3)}, ${maxX.toFixed(3)}] Y:[${minY.toFixed(3)}, ${maxY.toFixed(3)}], sample: ${namesPreview}`,
    );
  });

  if (components.length >= 2) {
    const master = components[0];
    const slave = components[1];
    let axisX = globalSymmetryAxisX;
    if (axisX === null) {
      axisX = (master.bounds.maxX + slave.bounds.minX) / 2;
    }
    console.log('Using axisX =', axisX.toFixed(3));

    const beamsOnAxis = [];
    const slaveBeamsToMirror = [];
    slave.component.forEach((beam) => {
      if (isBeamOnSymmetryAxis(beam, joints, axisX, SYMMETRY_TOLERANCE)) {
        beamsOnAxis.push(beam);
      } else {
        slaveBeamsToMirror.push(beam);
      }
    });
    console.log('slave beams total:', slave.component.length, '-> on axis:', beamsOnAxis.length, 'mirror candidates:', slaveBeamsToMirror.length);

    const masterMirrorTargets = master.component.filter(
      (beam) => !isBeamOnSymmetryAxis(beam, joints, axisX, SYMMETRY_TOLERANCE),
    );
    console.log('master beams eligible for mirroring:', masterMirrorTargets.length);

    if (components.length > 2) {
      const extra = components.slice(2);
      console.log('extra components (treated as orphans):', extra.length);
      extra.forEach((comp, idx) => {
        console.log(
          `  extra[${idx}] secondary count: ${comp.component.length}, bounds X:[${comp.bounds.minX.toFixed(3)}, ${comp.bounds.maxX.toFixed(3)}]`,
        );
      });
    }
  }
}

analyzeStory('2F');
