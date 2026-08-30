'use strict';

const path = require('path');
const webpack = require('webpack');
const CopyPlugin = require('copy-webpack-plugin');

/** @typedef {import('webpack').Configuration} WebpackConfig */

// Fable emits ESM into fable-out. Webpack consumes those plain JS modules and produces the
// CommonJS filenames that package.json exposes to VS Code and npm.

/** @type WebpackConfig */
const extensionConfig = {
  target: 'node',
  mode: 'none',
  entry: './fable-out/Extension/Extension.js',
  output: {
    path: path.resolve(__dirname, 'dist'),
    filename: 'extension.js',
    libraryTarget: 'commonjs2'
  },
  externals: {
    vscode: 'commonjs vscode'
  },
  experiments: {
    asyncWebAssembly: true
  },
  resolve: {
    extensions: ['.js']
  },
  module: {
    rules: [
      {
        test: /\.wasm$/,
        type: 'asset/resource',
        generator: {
          filename: '[name][ext]'
        }
      }
    ]
  },
  devtool: 'nosources-source-map',
  infrastructureLogging: {
    level: 'log'
  },
  plugins: [
    new CopyPlugin({
      patterns: [
        {
          from: path.resolve(__dirname, 'node_modules/web-tree-sitter/web-tree-sitter.wasm'),
          to: path.resolve(__dirname, 'dist/web-tree-sitter.wasm')
        }
      ]
    })
  ]
};

/** @type WebpackConfig */
const cliConfig = {
  target: 'node',
  mode: 'none',
  entry: './fable-out/Main.js',
  output: {
    path: path.resolve(__dirname, 'dist'),
    filename: 'cli.js',
    libraryTarget: 'commonjs2'
  },
  experiments: {
    asyncWebAssembly: true
  },
  resolve: {
    extensions: ['.js']
  },
  plugins: [
    // ADC: webpack strips the source shebang, and dist/cli.js is the npm "bin" entry run
    // directly by a shell, so it must start with one or `npx`/global installs fail.
    new webpack.BannerPlugin({ banner: '#!/usr/bin/env node', raw: true, entryOnly: true })
  ],
  devtool: 'nosources-source-map'
};

module.exports = [extensionConfig, cliConfig];
