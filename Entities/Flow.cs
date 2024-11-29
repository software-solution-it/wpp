namespace tests_.src.Domain.Entities
{
    using MongoDB.Bson;
    using MongoDB.Bson.Serialization.Attributes;
    using System.Collections.Generic;

    public class FlowWhatsapp
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("flowIn")]
        public string? FlowIn { get; set; }

        [BsonElement("name")]
        public string? Name { get; set; }

        [BsonElement("description")]
        public string? Description { get; set; }

        [BsonElement("sectorId")]
        public int? SectorId { get; set; }

        [BsonElement("nodes")]
        public List<NodeDTO>? Nodes { get; set; }

        [BsonElement("edges")]
        public List<EdgeDTO>? Edges { get; set; }
    }

    public class NodeDTO
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("label")]
        public string? Label { get; set; }

        [BsonElement("type")]
        public string? Type { get; set; }

        [BsonElement("blocks")]
        public List<BlockDTO>? Blocks { get; set; }

        // Alterado para aceitar um único objeto em vez de uma lista
        [BsonElement("menuOptions")]
        public MenuOptionDTO? MenuOptions { get; set; }

        [BsonElement("selectedTag")]
        public TagDTO? SelectedTag { get; set; }

        [BsonElement("condition")]
        public ConditionDTO? Condition { get; set; }

        // Adicionando o campo Position
        [BsonElement("position")]
        public PositionDTO? Position { get; set; }
    }

    public class PositionDTO
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("x")]
        public double? X { get; set; }

        [BsonElement("y")]
        public double? Y { get; set; }
    }

    public class BlockDTO
    {
        [BsonId]
        public string? Id { get; set; }

        [BsonElement("type")]
        public string? Type { get; set; }

        [BsonElement("content")]
        public string? Content { get; set; }

        [BsonElement("media")]
        public MediaDTO? Media { get; set; }

        [BsonElement("duration")]
        public int Duration { get; set; }
    }

    public class MenuOptionDTO
    {
        [BsonId]
        public string? Id { get; set; }

        [BsonElement("title")]
        public string? Title { get; set; }

        [BsonElement("content")]
        public List<string>? Content { get; set; }
    }

    public class TagDTO
    {
        [BsonId]
        public string? Id { get; set; }

        [BsonElement("tagId")]
        public int? TagId { get; set; }
    }

    public class ConditionDTO
    {
        [BsonId]
        public string? Id { get; set; }

        [BsonElement("variableId")]
        public int? VariableId { get; set; }

        [BsonElement("condition")]
        public string? Condition { get; set; }

        [BsonElement("value")]
        public string? Value { get; set; }
    }

    public class MediaDTO
    {
        [BsonId]
        public string? Id { get; set; }

        [BsonElement("name")]
        public string? Name { get; set; }

        [BsonElement("mimeType")]
        public string? MimeType { get; set; }

        [BsonElement("base64")]
        public string? Base64 { get; set; }
    }

    public class EdgeDTO
    {
        [BsonId]
        public string? Id { get; set; }

        [BsonElement("source")]
        public string? Source { get; set; }

        [BsonElement("target")]
        public string? Target { get; set; }

        [BsonElement("animated")]
        public bool? Animated { get; set; }

        [BsonElement("sourceHandle")]
        public string? SourceHandle { get; set; }

        [BsonElement("targetHandle")]
        public string? TargetHandle { get; set; }
    }
}
