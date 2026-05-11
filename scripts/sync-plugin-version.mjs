import { readFileSync, writeFileSync } from 'node:fs';
import { argv, exit } from 'node:process';

const version = argv[2];
if (!version || !/^\d+\.\d+\.\d+(-[\w.]+)?$/.test(version)) {
  console.error(`Invalid version argument: "${version}". Expected semver like 1.2.3 or 1.2.3-beta.1`);
  exit(1);
}

const updateJson = (path, mutator) => {
  const data = JSON.parse(readFileSync(path, 'utf8'));
  mutator(data);
  writeFileSync(path, JSON.stringify(data, null, 2) + '\n', 'utf8');
  console.log(`Updated ${path} -> ${version}`);
};

updateJson('.claude-plugin/plugin.json', (d) => {
  d.version = version;
});

updateJson('.claude-plugin/marketplace.json', (d) => {
  for (const p of d.plugins ?? []) {
    if (p.name === 'flaui-mcp') p.version = version;
  }
});
