﻿using System;
using Kudu.Core.SourceControl;
using Kudu.Core.SourceControl.Git;
using Xunit;
namespace Kudu.Core.Test
{
    public class GitRepositoryRepositoryTest
    {
        public void IsDiffHeaderReturnsTrueForValidDiffHeaders()
        {
        public void ConvertStatusUnknownStatusThrows()
        {
        public void ConvertStatusKnownStatuses()
        {
        public void ParseStatus()
        {
        public void PopulateStatusHandlesFilesWithSpaces()
        {
        public void ParseCommitParsesCommit()
        {
        public void ParseCommitWithMultipleCommitsParsesOneCommit()
        {
        public void Parse()
        {
        public void ParseDiffChunkHandlesFilesWithSpacesInName()
        {
        public void ParseDiffFileName()
        {
        private void AssertFile(ChangeSetDetail detail, string path, int? insertions = null, int? deletions = null, bool binary = false)
        {
            if (insertions != null)
            {
            if (deletions != null)
            {