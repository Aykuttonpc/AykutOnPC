using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace AykutOnPC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeEmbedding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AddColumn<Vector>(
                name: "Embedding",
                table: "KnowledgeEntries",
                type: "vector(768)",
                nullable: true);

            // HNSW index for cosine similarity search. EF doesn't model HNSW yet,
            // so we drop to raw SQL. vector_cosine_ops matches the `<=>` operator
            // used by SearchSimilarAsync. NULL embeddings are silently skipped.
            // Default HNSW params (m=16, ef_construction=64) are fine for ~50-200
            // entries; tune via WITH (m=...) when KB grows.
            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_KnowledgeEntries_Embedding_Hnsw""
                  ON ""KnowledgeEntries""
                  USING hnsw (""Embedding"" vector_cosine_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"DROP INDEX IF EXISTS ""IX_KnowledgeEntries_Embedding_Hnsw"";");

            migrationBuilder.DropColumn(
                name: "Embedding",
                table: "KnowledgeEntries");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
